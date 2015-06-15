namespace ServiceControl.Recoverability.Groups.Groupers
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    class MD5HashGroupIdGenerator : IGroupIdGenerator
    {
        public string GenerateId(string groupType, string groupName)
        {
            using(var cryptoProvider = new MD5CryptoServiceProvider())
            { 
                var rawBytes = Encoding.UTF8.GetBytes(groupType + groupName);
                var hashedBytes = cryptoProvider.ComputeHash(rawBytes);
                var converted = BitConverter.ToString(hashedBytes);
                return converted;
            }
        }
    }
}
