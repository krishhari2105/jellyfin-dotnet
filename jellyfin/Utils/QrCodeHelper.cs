using System;
using System.IO;
using QRCoder;
using Tizen.Applications;

namespace JellyfinTizen.Utils
{
    public static class QrCodeHelper
    {
        public static string GenerateQrCode(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                using var gen = new QRCodeGenerator();
                using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
                using var qr = new PngByteQRCode(data);
                byte[] qrPng = qr.GetGraphic(20, new byte[] { 0x14, 0x14, 0x14 }, new byte[] { 0xFF, 0xFF, 0xFF }, true);
                
                string dataDir = Application.Current.DirectoryInfo.Data;
                string qrDir = Path.Combine(dataDir, "tailscale-qr");
                Directory.CreateDirectory(qrDir);
                string qrPath = Path.Combine(qrDir, "auth-qr.png");
                
                File.WriteAllBytes(qrPath, qrPng);
                return qrPath;
            }
            catch (Exception ex)
            {
                JellyfinTizen.Core.TailscaleDebugLog.Add($"QrCodeHelper.GenerateQrCode error: {ex.Message}");
                return null;
            }
        }
    }
}
