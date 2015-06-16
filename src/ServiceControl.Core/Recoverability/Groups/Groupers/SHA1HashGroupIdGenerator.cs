namespace ServiceControl.Recoverability.Groups.Groupers
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    class SHA1HashGroupIdGenerator : IGroupIdGenerator
    {
        public string GenerateId(string groupType, string groupName)
        {
            using (var cryptoProvider = new SHA1CryptoServiceProvider())
            {
                var rawBytes = Encoding.UTF8.GetBytes(groupType + groupName);
                var hashedBytes = cryptoProvider.ComputeHash(rawBytes);
                var converted = BitConverter.ToString(hashedBytes).Replace("-", String.Empty);
                return converted;
            }
        }
    }
}
