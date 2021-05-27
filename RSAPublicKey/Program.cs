﻿using System;
using System.IO;

/**
 *  Generates an RSA-OAEP 2048 public key from modulus and exponent
 *  Used to convert the powerbi gateway public key into a PEM format for use in other programs
 *  Source: https://stackoverflow.com/questions/28406888/c-sharp-rsa-public-key-output-not-correct/28407693#28407693
 */  

namespace RSAPublicKey
{
    class Program
    {
        static void Main(string[] args)
        {
			/** 
			 * Update the values below with the publicKey.modulus and publicKey.exponent properties from getGateways or getGateway api calls
			 * The corresponding public key will be written to the file path below
			 * Modulus and Exponent should be base64 strings (this is what the powerbi api returns)
			 */
			var pemFilePath = "C:\gateway.pem";
			var modulus = "base64modulus==";
			var exponent = "AAAA";


			// start actual work
			var modulusBytes = Convert.FromBase64String(modulus);
			var exponentBytes = Convert.FromBase64String(exponent);

			TextWriter pemFileWriter = new StreamWriter(pemFilePath);
			ExportPublicKey(modulusBytes, exponentBytes, pemFileWriter);
			pemFileWriter.Flush();
			pemFileWriter.Close();

			Console.WriteLine("Successfully generated RSA PEM File");
			Console.WriteLine("File Path: " + pemFilePath);
			Console.WriteLine("Modulus: " + modulus);
			Console.WriteLine("Exponent: " + exponent);
		}

		private static void ExportPublicKey(byte[] modulus, byte[] exponent, TextWriter outputStream)
		{
			// var parameters = csp.ExportParameters(false);
			using (var stream = new MemoryStream())
			{
				var writer = new BinaryWriter(stream);
				writer.Write((byte)0x30); // SEQUENCE
				using (var innerStream = new MemoryStream())
				{
					var innerWriter = new BinaryWriter(innerStream);
					innerWriter.Write((byte)0x30); // SEQUENCE
					EncodeLength(innerWriter, 13);
					innerWriter.Write((byte)0x06); // OBJECT IDENTIFIER
					var rsaEncryptionOid = new byte[] { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01 };
					EncodeLength(innerWriter, rsaEncryptionOid.Length);
					innerWriter.Write(rsaEncryptionOid);
					innerWriter.Write((byte)0x05); // NULL
					EncodeLength(innerWriter, 0);
					innerWriter.Write((byte)0x03); // BIT STRING
					using (var bitStringStream = new MemoryStream())
					{
						var bitStringWriter = new BinaryWriter(bitStringStream);
						bitStringWriter.Write((byte)0x00); // # of unused bits
						bitStringWriter.Write((byte)0x30); // SEQUENCE
						using (var paramsStream = new MemoryStream())
						{
							var paramsWriter = new BinaryWriter(paramsStream);
							EncodeIntegerBigEndian(paramsWriter, modulus); // Modulus
							EncodeIntegerBigEndian(paramsWriter, exponent); // Exponent
							var paramsLength = (int)paramsStream.Length;
							EncodeLength(bitStringWriter, paramsLength);
							bitStringWriter.Write(paramsStream.GetBuffer(), 0, paramsLength);
						}
						var bitStringLength = (int)bitStringStream.Length;
						EncodeLength(innerWriter, bitStringLength);
						innerWriter.Write(bitStringStream.GetBuffer(), 0, bitStringLength);
					}
					var length = (int)innerStream.Length;
					EncodeLength(writer, length);
					writer.Write(innerStream.GetBuffer(), 0, length);
				}

				var base64 = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length).ToCharArray();
				outputStream.WriteLine("-----BEGIN PUBLIC KEY-----");
				for (var i = 0; i < base64.Length; i += 64)
				{
					outputStream.WriteLine(base64, i, Math.Min(64, base64.Length - i));
				}
				outputStream.WriteLine("-----END PUBLIC KEY-----");
			}
		}

		private static void EncodeLength(BinaryWriter stream, int length)
		{
			if (length < 0) throw new ArgumentOutOfRangeException("length", "Length must be non-negative");
			if (length < 0x80)
			{
				// Short form
				stream.Write((byte)length);
			}
			else
			{
				// Long form
				var temp = length;
				var bytesRequired = 0;
				while (temp > 0)
				{
					temp >>= 8;
					bytesRequired++;
				}
				stream.Write((byte)(bytesRequired | 0x80));
				for (var i = bytesRequired - 1; i >= 0; i--)
				{
					stream.Write((byte)(length >> (8 * i) & 0xff));
				}
			}
		}

		private static void EncodeIntegerBigEndian(BinaryWriter stream, byte[] value, bool forceUnsigned = true)
		{
			stream.Write((byte)0x02); // INTEGER
			var prefixZeros = 0;
			for (var i = 0; i < value.Length; i++)
			{
				if (value[i] != 0) break;
				prefixZeros++;
			}
			if (value.Length - prefixZeros == 0)
			{
				EncodeLength(stream, 1);
				stream.Write((byte)0);
			}
			else
			{
				if (forceUnsigned && value[prefixZeros] > 0x7f)
				{
					// Add a prefix zero to force unsigned if the MSB is 1
					EncodeLength(stream, value.Length - prefixZeros + 1);
					stream.Write((byte)0);
				}
				else
				{
					EncodeLength(stream, value.Length - prefixZeros);
				}
				for (var i = prefixZeros; i < value.Length; i++)
				{
					stream.Write(value[i]);
				}
			}
		}
	}
}
