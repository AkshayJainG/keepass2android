using System;
using System.Collections.Generic;
using System.IO;
using Android.App;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Keepass2android.Javafilestorage;
using Exception = System.Exception;
using FileNotFoundException = Java.IO.FileNotFoundException;

namespace keepass2android.Io
{
	public abstract class JavaFileStorage: IFileStorage
	{
		public abstract IEnumerable<string> SupportedProtocols { get; }

		private readonly IJavaFileStorage _jfs;
		private readonly IKp2aApp _app;

		public JavaFileStorage(IJavaFileStorage jfs, IKp2aApp app)
		{
			_jfs = jfs;
			_app = app;
		}

		public void DeleteFile(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
		{
			return false;

			//commented because this currently might use the network which is not permitted here
			/*try
			{
				return Jfs.CheckForFileChangeFast(ioc.Path, previousFileVersion);
			}
			catch (Java.Lang.Exception e)
			{
				throw LogAndConvertJavaException(e);
			}*/

		}

		public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
		{
			try
			{
				return Jfs.GetCurrentFileVersionFast(ioc.Path);
			}
			catch (Java.Lang.Exception e)
			{
				throw LogAndConvertJavaException(e);
			} 
		}

		public Stream OpenFileForRead(IOConnectionInfo ioc)
		{
			try
			{
				return Jfs.OpenFileForRead(IocToPath(ioc));
			}
			catch (FileNotFoundException e)
			{
				throw new System.IO.FileNotFoundException(e.Message, e);
			}
			catch (Java.Lang.Exception e)
			{
				throw LogAndConvertJavaException(e);
			}
		}


		private Exception LogAndConvertJavaException(Java.Lang.Exception e)
		{
			Kp2aLog.Log(e.Message);
			var ex = new Exception(e.LocalizedMessage ?? 
				e.Message ?? 
				_app.GetResourceString(UiStringKey.ErrorOcurred)+e, e);
			return ex; 
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			return new JavaFileStorageWriteTransaction(IocToPath(ioc), useFileTransaction, this);
		}

		public IFileStorageSetup RequiredSetup 
		{
			get
			{
				if (Jfs.IsConnected)
					return null;
				return new JavaFileStorageSetup(this);
			}
		}

		internal IJavaFileStorage Jfs
		{
			get { return _jfs; }
		}

		public class JavaFileStorageSetup : IFileStorageSetup, IFileStorageSetupOnResume
		{
			private readonly JavaFileStorage _javaFileStorage;

			public JavaFileStorageSetup(JavaFileStorage javaFileStorage)
			{
				_javaFileStorage = javaFileStorage;
			}

			public bool TrySetup(Activity activity)
			{
				try
				{
					return _javaFileStorage.Jfs.TryConnect(activity);
				}
				catch (Java.Lang.Exception e)
				{
					throw _javaFileStorage.LogAndConvertJavaException(e);
				}
			}

			public bool TrySetupOnResume(Activity activity)
			{
				try
				{
					_javaFileStorage.Jfs.OnResume();
					return _javaFileStorage.Jfs.IsConnected;
				}
				catch (Java.Lang.Exception e)
				{
					throw _javaFileStorage.LogAndConvertJavaException(e);
				}
			}
		}

		class JavaFileStorageWriteTransaction: IWriteTransaction
		{
			private readonly string _path;
			private readonly bool _useFileTransaction;
			private readonly JavaFileStorage _javaFileStorage;
			private MemoryStream _memoryStream;

			public JavaFileStorageWriteTransaction(string path, bool useFileTransaction, JavaFileStorage javaFileStorage)
			{
				_path = path;
				_useFileTransaction = useFileTransaction;
				_javaFileStorage = javaFileStorage;
			}

			public void Dispose()
			{
				_memoryStream.Dispose();
			}

			public Stream OpenFile()
			{
				_memoryStream = new MemoryStream();
				return _memoryStream;
			}

			public void CommitWrite()
			{
				try
				{
					_javaFileStorage.Jfs.UploadFile(_path, _memoryStream.ToArray(), _useFileTransaction);
				}
				catch (Java.Lang.Exception e)
				{
					throw _javaFileStorage.LogAndConvertJavaException(e);
				}
			}
		}

		public bool CompleteIoId()
		{
			throw new NotImplementedException();
		}

		public bool? FileExists()
		{
			throw new NotImplementedException();
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			return UrlUtil.StripExtension(
					UrlUtil.GetFileName(IocToPath(ioc)));
		}

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			return false;
		}

		private static string IocToPath(IOConnectionInfo ioc)
		{
			int protocolLength = ioc.Path.IndexOf("://", StringComparison.Ordinal);

			if (protocolLength < 0)
				return ioc.Path;
			else
				return ioc.Path.Substring(protocolLength + 3);
		}
	}
}