namespace ServiceControlInstaller.CustomActions.Extensions
{
    using System;
    using System.Linq;
    using System.Text;

    static class GuidExtensions
    {
        /// <summary>
        /// For some reason MS converts MSI product code and upgrade code GUIDs when using them in the registry.
        /// For instance a product code "{12BDB3E7-3A70-410A-A2C0-43037E33E3E6}" is represented as 7E3BDB2107A3A0142A0C3430E7333E6E in the registry key:
        /// `HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Installer\Products\7E3BDB2107A3A0142A0C3430E7333E6E`
        /// This flip method will convert the GUID which can then be string formatted using "N" to get registry format.
        /// The algorithm is reversible, so if you flip the GUID again your back to where you started.
        /// </summary>
        /// <param name="guid">Guid to Flip</param>
        /// <returns>Flipped Guid</returns>
        public static Guid Flip(this Guid guid)
        {
            var sb = new StringBuilder();
            var parts = guid.ToString("D").Split('-');
            foreach (var part in parts.Take(3))
            {
                sb.Append(part.Reverse());
            }

            foreach (var chunk in parts.Skip(3).SelectMany(part => part.Chunks(2)))
            {
                sb.Append(chunk.Reverse());
            }
            return new Guid(sb.ToString());
        }
    }
}