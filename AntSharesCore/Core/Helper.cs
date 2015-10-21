﻿using AntShares.Core.Scripts;
using AntShares.Cryptography;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AntShares.Core
{
    public static class Helper
    {
        private const byte CoinVersion = 0x17;

        public static byte[] GetHashForSigning(this ISignable signable)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                signable.SerializeUnsigned(writer);
                writer.Flush();
                return ms.ToArray().Sha256();
            }
        }

        internal static byte[] Sign(this ISignable signable, byte[] prikey, byte[] pubkey)
        {
            const int ECDSA_PRIVATE_P256_MAGIC = 0x32534345;
            prikey = BitConverter.GetBytes(ECDSA_PRIVATE_P256_MAGIC).Concat(BitConverter.GetBytes(32)).Concat(pubkey).Concat(prikey).ToArray();
            using (CngKey key = CngKey.Import(prikey, CngKeyBlobFormat.EccPrivateBlob))
            using (ECDsaCng ecdsa = new ECDsaCng(key))
            {
                return ecdsa.SignHash(signable.GetHashForSigning());
            }
        }

        public static string ToAddress(this UInt160 hash)
        {
            byte[] data = new byte[] { CoinVersion }.Concat(hash.ToArray()).ToArray();
            return Base58.Encode(data.Concat(data.Sha256().Sha256().Take(4)).ToArray());
        }

        public static UInt160 ToScriptHash(this string address)
        {
            byte[] data = Base58.Decode(address);
            if (data.Length != 25)
                throw new FormatException();
            if (data[0] != CoinVersion)
                throw new FormatException();
            if (!data.Take(21).Sha256().Sha256().Take(4).SequenceEqual(data.Skip(21)))
                throw new FormatException();
            return new UInt160(data.Skip(1).Take(20).ToArray());
        }

        internal static VerificationResult VerifySignature(this ISignable signable)
        {
            UInt160[] hashes;
            try
            {
                hashes = signable.GetScriptHashesForVerifying();
            }
            catch (InvalidOperationException)
            {
                return VerificationResult.LackOfInformation;
            }
            if (hashes.Length != signable.Scripts.Length)
                return VerificationResult.InvalidSignature;
            for (int i = 0; i < hashes.Length; i++)
            {
                if (hashes[i] != signable.Scripts[i].RedeemScript.ToScriptHash()) return VerificationResult.InvalidSignature;
                ScriptEngine engine = new ScriptEngine(signable.Scripts[i], signable.GetHashForSigning());
                if (!engine.Execute()) return VerificationResult.InvalidSignature;
            }
            return VerificationResult.OK;
        }
    }
}
