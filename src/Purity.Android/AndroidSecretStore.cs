using System.Text;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using Purity.Reminders;


namespace Purity.Android
{
	/// <summary>
	/// A sign-in kept encrypted in Purity's own storage, by a key Android's keystore holds and never
	/// lets out. Another app can't read it, and a copy of the file is no use without this phone.
	/// </summary>
	internal sealed class AndroidSecretStore(string file) : ISecretStore
	{
		public string? Read()
		{
			try
			{
				if (!File.Exists(file))
					return null;

				var stored = File.ReadAllBytes(file);
				using var cipher = Cipher.GetInstance(Transformation)!;
				cipher.Init(CipherMode.DecryptMode, Key(), new GCMParameterSpec(TagBits, stored, 0, IvLength));
				return Encoding.UTF8.GetString(cipher.DoFinal(stored, IvLength, stored.Length - IvLength)!);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or Java.Lang.Exception)
			{
				return null;						// not readable any more, as after the phone was restored: the person signs in again
			}
		}

		public bool Write(string secret)
		{
			try
			{
				using var cipher = Cipher.GetInstance(Transformation)!;
				cipher.Init(CipherMode.EncryptMode, Key());
				var iv = cipher.GetIV()!;
				var sealedText = cipher.DoFinal(Encoding.UTF8.GetBytes(secret))!;
				Directory.CreateDirectory(Path.GetDirectoryName(file)!);
				var temp = file + ".tmp";
				File.WriteAllBytes(temp, iv.Concat(sealedText).ToArray());
				File.Move(temp, file, overwrite: true);
				return true;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or Java.Lang.Exception)
			{
				return false;
			}
		}

		public void Delete()
		{
			try
			{
				File.Delete(file);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// nothing more can be done
			}
		}


		/// <summary>The keystore's key, made the first time it's needed</summary>
		private static IKey Key()
		{
			var store = KeyStore.GetInstance(KeyStoreName)!;
			store.Load(null);
			if (store.GetKey(Alias, null) is { } existing)
				return existing;

			var maker = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, KeyStoreName)!;
			maker.Init(new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
				.SetBlockModes(KeyProperties.BlockModeGcm)
				.SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
				.SetKeySize(256)
				.Build());
			return maker.GenerateKey()!;
		}


		private const string KeyStoreName = "AndroidKeyStore";
		private const string Alias = "purity-sign-ins";
		private const string Transformation = "AES/GCM/NoPadding";
		private const int IvLength = 12;
		private const int TagBits = 128;
	}
}
