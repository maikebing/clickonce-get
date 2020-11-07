﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ClickOnceGet.Server.Services;
using Xunit;

namespace ClickOnceGet.Test
{
    public class CertificateValidaterTest : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => HttpClientFactory.Create();

        private static byte[] PublicKeyModule = new byte[] { 0x00, 0xab, 0x79, 0xf1, 0xbb, 0xc4, 0x15, 0xba, 0x39, 0x70, 0x34, 0x03, 0xcf, 0xac, 0xd3, 0x4f, 0xea, 0x6f, 0x4f, 0x17, 0x93, 0x24, 0x44, 0xf0, 0x48, 0xcf, 0x0b, 0xe9, 0x2c, 0xed, 0x36, 0x9f, 0xee, 0x0b, 0x70, 0x18, 0xa2, 0xd0, 0x7e, 0x8c, 0xc6, 0x4d, 0xea, 0xd4, 0x62, 0x5d, 0xfc, 0xc4, 0x57, 0xba, 0x57, 0xa2, 0xa6, 0xe4, 0xf8, 0xbf, 0xe7, 0xc7, 0x57, 0xee, 0xed, 0x29, 0x49, 0x03, 0xce, 0x97, 0x8f, 0x8a, 0x74, 0xc9, 0x16, 0x16, 0xdf, 0x51, 0x46, 0xc9, 0x2b, 0xbc, 0x98, 0x7d, 0x6d, 0xe1, 0xe6, 0x47, 0xcb, 0x86, 0x4c, 0x81, 0x43, 0x67, 0x3c, 0xb5, 0x3b, 0x34, 0x42, 0x4b, 0xcb, 0xe4, 0xf7, 0x1c, 0x5e, 0x90, 0x9e, 0x42, 0xc7, 0x0c, 0x03, 0x10, 0x59, 0x66, 0xfa, 0x5d, 0x23, 0x47, 0xf1, 0x8a, 0xff, 0x75, 0xc0, 0x90, 0x95, 0x48, 0xf0, 0xc1, 0x09, 0x0a, 0x4a, 0x1d, 0xb0, 0x02, 0xa1, 0xbf, 0x3e, 0x3e, 0x4f, 0x5e, 0xfe, 0xfd, 0xfd, 0xc9, 0x84, 0x82, 0x64, 0x73, 0xa8, 0xed, 0x1d, 0xd0, 0x0f, 0x6f, 0x1a, 0xdc, 0x46, 0x05, 0xad, 0x06, 0x69, 0x1e, 0x47, 0xfa, 0xf3, 0xf0, 0xd0, 0xae, 0xcf, 0x22, 0x01, 0xa5, 0x77, 0x0b, 0x00, 0xf1, 0xe9, 0x0f, 0xb8, 0xf1, 0x1e, 0x72, 0x88, 0x7b, 0x94, 0x88, 0xa6, 0x13, 0xb4, 0x23, 0xe6, 0x18, 0x12, 0x6c, 0x71, 0x1d, 0x69, 0x84, 0x38, 0xb8, 0xd9, 0xca, 0x10, 0xb9, 0x84, 0xa4, 0xb3, 0x10, 0x55, 0xbc, 0xbd, 0x8c, 0x14, 0x04, 0x89, 0x50, 0x51, 0xa4, 0x30, 0x60, 0xbb, 0x66, 0x8a, 0x3b, 0xe0, 0xd4, 0x1f, 0x2e, 0x3a, 0x22, 0x29, 0x52, 0x6c, 0x1a, 0xc9, 0x74, 0x01, 0x60, 0x8c, 0x34, 0x15, 0xe3, 0xa6, 0x6c, 0x91, 0x04, 0x24, 0xef, 0x91, 0x5d, 0x70, 0x2b, 0x7c, 0xe3, 0x26, 0x38, 0x9d, 0x1b, 0x83, 0x3b, 0x13 };

        private string PathOf(string fileName) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files", fileName);

        public CertificateValidaterTest()
        {
            // GitHub supports only TLS v1.2.
            // https://githubengineering.com/crypto-removal-notice/
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        [Fact]
        public void GetModuleFromSSHPublicKeyString_Test()
        {
            var pubKey = File.ReadAllText(PathOf("id_rsa.pub"));

            new CertificateValidater(this)
                .GetModuleFromSSHPublicKeyString(pubKey)
                .Last()
                .Is(PublicKeyModule);
        }

        [Fact]
        public void GetModuleFromSSHPublicKeyString_From_NullStr_Test()
        {
            new CertificateValidater(this)
                .GetModuleFromSSHPublicKeyString(null)
                .Count().Is(0);
        }

        [Fact]
        public void GetModuleFromX509CertificateFile_Test()
        {
            new CertificateValidater(this)
                .GetModuleFromX509CertificateFile(PathOf("id_rsa.cer"))
                .Is(PublicKeyModule);
        }

        [Fact]
        public void EqualsPublicKey_True_Test()
        {
            var sshPubKeyString = File.ReadAllText(PathOf("id_rsa.pub"));
            var pathOfCertFile = PathOf("id_rsa.cer");

            new CertificateValidater(this).EqualsPublicKey(sshPubKeyString, pathOfCertFile)
                .IsTrue();
        }

        [Fact]
        public void EqualsPublicKey_False_Test()
        {
            var sshPubKeyString = File.ReadAllText(PathOf("id_rsa.pub"));
            var pathOfCertFile = PathOf("another.cer");

            new CertificateValidater(this).EqualsPublicKey(sshPubKeyString, pathOfCertFile)
                .IsFalse();
        }

        [Fact]
        public void EqualsPublicKey_NullSshPubKeyStr_False_Test()
        {
            var sshPubKeyString = default(string);
            var pathOfCertFile = PathOf("id_rsa.cer");

            new CertificateValidater(this).EqualsPublicKey(sshPubKeyString, pathOfCertFile)
                .IsFalse();
        }

        [Fact]
        public async Task GetSSHPubKeyStrOfGitHubAccountAsync_Test()
        {
            var certificateValidater = new CertificateValidater(this);
            var pubKeysString = await certificateValidater.GetSSHPubKeyStrOfGitHubAccountAsync("jsakamoto");

            var _1stPubKeyStr = pubKeysString.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).First();
            var pubKeyParts = _1stPubKeyStr.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            pubKeyParts.First().Is("ssh-rsa");

            var pubKeyBin = Convert.FromBase64String(pubKeyParts.Last());
            string.Join("", pubKeyBin
                .Skip(4) // byte size of algorithm identifier
                .Take(7) // "ssh-rsa"
                .Select(n => (char)n))
                .Is("ssh-rsa");
        }

        [Fact]
        public async Task GetSSHPubKeyStrOfGitHubAccountAsync_HTTPError_Test()
        {
            var nousername = Guid.NewGuid().ToString("N");
            var certificateValidater = new CertificateValidater(this);
            var pubKeysString = await certificateValidater.GetSSHPubKeyStrOfGitHubAccountAsync(nousername);

            pubKeysString.IsNull();
        }

    }
}

