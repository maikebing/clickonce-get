﻿#nullable enable
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Xml.Linq;
using ClickOnceGet.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Toolbelt.Drawing;

namespace ClickOnceGet.Server.Services
{
    public class ClickOnceAppContentManager
    {
        private IClickOnceFileRepository ClickOnceFileRepository { get; }

        private CertificateValidater CertificateValidater { get; }

        private IWebHostEnvironment WebHostEnv { get; }

        public ClickOnceAppContentManager(
            IClickOnceFileRepository clickOnceFileRepository,
            CertificateValidater certificateValidater,
            IWebHostEnvironment webHostEnv)
        {
            ClickOnceFileRepository = clickOnceFileRepository;
            CertificateValidater = certificateValidater;
            WebHostEnv = webHostEnv;
        }

        public async Task<byte[]?> UpdateCertificateInfoAsync(ClickOnceAppInfo appInfo)
        {
            appInfo.SignedByPublisher = false;

            var certBin = default(byte[]);
            var tmpPath = default(string);
            try
            {
                tmpPath = ExtractEntryPointCommandFile(appInfo.Name);
                if (tmpPath != null)
                {
                    var cert = X509Certificate.CreateFromSignedFile(tmpPath);
                    if (cert != null)
                    {
                        certBin = cert.GetRawCertData();
                        this.ClickOnceFileRepository.SaveFileContent(appInfo.Name, ".cer", certBin);
                        await this.UpdateSignedByPublisherAsync(appInfo, () => cert);
                    }
                }
            }
            catch (CryptographicException) { }
            finally { if (tmpPath != null) File.Delete(tmpPath); }

            appInfo.HasCodeSigning = certBin != null;
            return certBin;
        }

        public async Task UpdateSignedByPublisherAsync(ClickOnceAppInfo appInfo)
        {
            if (appInfo.HasCodeSigning.HasValue == false)
            {
                await UpdateCertificateInfoAsync(appInfo);
            }
            else
            {
                await UpdateSignedByPublisherAsync(appInfo, () =>
                {
                    var tmpPath = Path.Combine(WebHostEnv.ContentRootPath, "App_Data", $"{Guid.NewGuid():N}.cer");
                    try
                    {
                        var certBin = this.ClickOnceFileRepository.GetFileContent(appInfo.Name, ".cer");
                        File.WriteAllBytes(tmpPath, certBin);
                        var cert = X509Certificate.CreateFromCertFile(tmpPath);
                        return cert;
                    }
                    finally { try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { } }
                });
            }
        }

        private async Task UpdateSignedByPublisherAsync(ClickOnceAppInfo appInfo, Func<X509Certificate> getCert)
        {
            appInfo.SignedByPublisher = false;
            if (string.IsNullOrEmpty(appInfo.PublisherName)) return;

            var sshPubKeyStr = await this.CertificateValidater.GetSSHPubKeyStrOfGitHubAccountAsync(appInfo.PublisherName);
            var cert = getCert();
            appInfo.SignedByPublisher = CertificateValidater.EqualsPublicKey(sshPubKeyStr, cert);
        }

        private string? ExtractEntryPointCommandFile(string appId)
        {
            var commandPath = GetEntryPointCommandPath(appId);
            if (commandPath == null) return null;

            var commandBytes = this.ClickOnceFileRepository.GetFileContent(appId, commandPath);
            if (commandBytes == null) return null;

            var tmpPath = Path.Combine(WebHostEnv.ContentRootPath, "App_Data", $"{Guid.NewGuid():N}.exe");
            File.WriteAllBytes(tmpPath, commandBytes);
            return tmpPath;
        }

        public string? GetEntryPointCommandPath(string appId)
        {
            var dotAppBytes = this.ClickOnceFileRepository.GetFileContent(appId, appId + ".application");
            if (dotAppBytes == null) return null;

            // parse .application
            var ns_asmv2 = "urn:schemas-microsoft-com:asm.v2";
            var dotApp = XDocument.Load(new MemoryStream(dotAppBytes));
            var codebasePath =
                (from node in dotApp.Descendants(XName.Get("dependentAssembly", ns_asmv2))
                 let dependencyType = node.Attribute("dependencyType")
                 where dependencyType != null
                 where dependencyType.Value == "install"
                 select node.Attribute("codebase")?.Value).FirstOrDefault();
            if (codebasePath == null) return null;

            // parse .manifest to detect .exe file path
            var mnifestBytes = this.ClickOnceFileRepository.GetFileContent(appId, codebasePath);
            if (mnifestBytes == null) return null;
            var manifest = XDocument.Load(new MemoryStream(mnifestBytes));
            var commandName =
                (from entryPoint in manifest.Descendants(XName.Get("entryPoint", ns_asmv2))
                 from commandLine in entryPoint.Descendants(XName.Get("commandLine", ns_asmv2))
                 let file = commandLine.Attribute("file")
                 where file != null
                 select file.Value).FirstOrDefault();
            if (commandName == null) return null;

            // load command(.exe) content binary.
            var pathParts = codebasePath.Split('\\');
            var commandPath = string.Join("\\", pathParts.Take(pathParts.Length - 1).Concat(new[] { commandName + ".deploy" }));

            return commandPath;
        }

        public byte[]? GetIcon(string appId, int pxSize = 48)
        {
            // extract icon from .exe
            // note: `IconExtractor` use LoadLibrary Win32API, so I need save the command binary into file.
            var tmpPath = ExtractEntryPointCommandFile(appId);
            if (tmpPath == null) return null;

            try
            {
                using var msIco = new MemoryStream();
                using var msPng = new MemoryStream();

                IconExtractor.Extract1stIconTo(tmpPath, msIco);
                if (msIco.Length == 0) return null;

                msIco.Seek(0, SeekOrigin.Begin);
                using var icon = new Icon(msIco, pxSize, pxSize);

                var iconBmp = icon.ToBitmap();
                var iconSize = iconBmp.Size.Width;
                if (iconSize < pxSize)
                {
                    //var margin = (pxSize - iconSize) / 2;
                    var newBmp = new Bitmap(width: pxSize, height: pxSize);
                    using (var g = Graphics.FromImage(newBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(iconBmp, 0, 0, pxSize, pxSize);
                    }
                    iconBmp.Dispose();
                    iconBmp = newBmp;
                }
                iconBmp.Save(msPng, ImageFormat.Png);
                iconBmp.Dispose();

                return msPng.ToArray();
            }
            catch (OutOfMemoryException)
            {
                return null;
            }
            finally
            {
                File.Delete(tmpPath);
            }
        }
    }
}