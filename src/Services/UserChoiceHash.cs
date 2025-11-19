using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Runtime.Versioning;

namespace ExtractNow.Services
{
    [SupportedOSPlatform("windows")]
    internal static class UserChoiceHash
    {
        public static string Generate(string extension, string progId, string userSid, DateTime timestamp)
        {
            string userExperience = "User Choice set via Windows User Experience {D18B6DD5-6124-4341-9318-804003BAFA0B}";
            long fileTime = timestamp.ToFileTimeUtc();
            
            uint hi = (uint)(fileTime >> 32);
            uint low = (uint)(fileTime & 0xFFFFFFFF);
            string dateTimeHex = (hi.ToString("X8") + low.ToString("X8")).ToLower();
            
            string baseInfo = $"{extension.ToLower()}{userSid.ToLower()}{progId.ToLower()}{dateTimeHex}{userExperience.ToLower()}";
            
            return ComputeHash(baseInfo);
        }
        
        public static string GetCurrentUserSid()
        {
            try
            {
                return WindowsIdentity.GetCurrent().User?.Value?.ToLower() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
        
        private static string ComputeHash(string baseInfo)
        {
            byte[] bytesBaseInfo = Encoding.Unicode.GetBytes(baseInfo);
            byte[] bytesFinal = new byte[bytesBaseInfo.Length + 2];
            Array.Copy(bytesBaseInfo, bytesFinal, bytesBaseInfo.Length);
            
            byte[] bytesMD5;
            using (var md5 = MD5.Create())
            {
                bytesMD5 = md5.ComputeHash(bytesFinal);
            }
            
            int lengthBase = (baseInfo.Length * 2) + 2;
            int length = ((lengthBase & 4) <= 1 ? 0 : 1) + (lengthBase >> 2) - 1;
            
            if (length <= 1)
            {
                return string.Empty;
            }
            
            // Ensure we have enough data for processing
            int minDataSize = ((length >> 1) + 1) * 8;
            if (bytesFinal.Length < minDataSize)
            {
                Array.Resize(ref bytesFinal, minDataSize);
            }
            
            byte[] outHash = new byte[16];
            
            // First loop
            {
                int md51 = (BitConverter.ToInt32(bytesMD5, 0) | 1) + 0x69FB0000;
                int md52 = (BitConverter.ToInt32(bytesMD5, 4) | 1) + 0x13DB0000;
                int cache = 0;
                int outHash1 = 0;
                int pData = 0;
                int counter = (length >> 1) + 1;
                
                while (counter > 0)
                {
                    if (pData + 4 > bytesFinal.Length) break;
                    
                    int r0 = (BitConverter.ToInt32(bytesFinal, pData) + outHash1);
                    int r1Value = (pData + 4 < bytesFinal.Length) ? BitConverter.ToInt32(bytesFinal, pData + 4) : 0;
                    pData += 8;
                    
                    int r1 = (int)((long)r0 * md51);
                    int r2 = (int)((0x79F8A395L * r1) + (0x689B6B9FL * ShiftRight(r1, 16)));
                    int r3 = (int)((0xEA970001L * r2) - (0x3C101569L * ShiftRight(r2, 16)));
                    int r4 = r3 + r1Value;
                    int r5 = cache + r3;
                    int r6 = (int)((long)r4 * md52);
                    int r7 = (int)((0x59C3AF2DL * r6) - (0x2232E0F1L * ShiftRight(r6, 16)));
                    outHash1 = (int)((0x1EC90001L * r7) + (0x35BD1EC9L * ShiftRight(r7, 16)));
                    
                    cache = r5 + outHash1;
                    counter--;
                }
                
                Array.Copy(BitConverter.GetBytes(outHash1), 0, outHash, 0, 4);
                Array.Copy(BitConverter.GetBytes(cache), 0, outHash, 4, 4);
            }
            
            // Second loop
            {
                int md51 = BitConverter.ToInt32(bytesMD5, 0) | 1;
                int md52 = BitConverter.ToInt32(bytesMD5, 4) | 1;
                int cache = 0;
                int outHash1 = 0;
                int pData = 0;
                int counter = (length >> 1) + 1;
                
                while (counter > 0)
                {
                    if (pData + 4 > bytesFinal.Length) break;
                    
                    int r0 = BitConverter.ToInt32(bytesFinal, pData) + outHash1;
                    int r1Value = (pData + 4 < bytesFinal.Length) ? BitConverter.ToInt32(bytesFinal, pData + 4) : 0;
                    pData += 8;
                    
                    int r1 = (int)((long)r0 * md51);
                    int r2 = (int)((0xB1110000L * r1) - (0x30674EEFL * ShiftRight(r1, 16)));
                    int r3 = (int)((0x5B9F0000L * r2) - (0x78F7A461L * ShiftRight(r2, 16)));
                    int r4 = (int)((0x12CEB96DL * ShiftRight(r3, 16)) - (0x46930000L * r3));
                    int r5 = (int)((0x1D830000L * r4) + (0x257E1D83L * ShiftRight(r4, 16)));
                    int r6 = (int)((long)md52 * ((long)r5 + r1Value));
                    int r7 = (int)((0x16F50000L * r6) - (0x5D8BE90BL * ShiftRight(r6, 16)));
                    int r8 = (int)((0x96FF0000L * r7) - (0x2C7C6901L * ShiftRight(r7, 16)));
                    int r9 = (int)((0x2B890000L * r8) + (0x7C932B89L * ShiftRight(r8, 16)));
                    outHash1 = (int)((0x9F690000L * r9) - (0x405B6097L * ShiftRight(r9, 16)));
                    
                    cache = outHash1 + cache + r5;
                    counter--;
                }
                
                Array.Copy(BitConverter.GetBytes(outHash1), 0, outHash, 8, 4);
                Array.Copy(BitConverter.GetBytes(cache), 0, outHash, 12, 4);
            }
            
            byte[] outHashBase = new byte[8];
            int hashValue1 = BitConverter.ToInt32(outHash, 8) ^ BitConverter.ToInt32(outHash, 0);
            int hashValue2 = BitConverter.ToInt32(outHash, 12) ^ BitConverter.ToInt32(outHash, 4);
            Array.Copy(BitConverter.GetBytes(hashValue1), 0, outHashBase, 0, 4);
            Array.Copy(BitConverter.GetBytes(hashValue2), 0, outHashBase, 4, 4);
            
            return Convert.ToBase64String(outHashBase);
        }
        
        private static int ShiftRight(int value, int count)
        {
            if ((value & 0x80000000) != 0)
            {
                return ((value >> count) ^ unchecked((int)0xFFFF0000));
            }
            return value >> count;
        }
    }
}
