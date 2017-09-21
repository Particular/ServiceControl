namespace Particular.Licensing
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.Xml;
    using System.Xml;

    class LicenseVerifier
    {
        public static bool TryVerify(string licenseText, out Exception failure)
        {
            try
            {
                Verify(licenseText);

                failure = null;

                return true;
            }
            catch (Exception ex)
            {
                failure = ex;
                return false;
            }
        }

        public static void Verify(string licenseText)
        {
            if (string.IsNullOrEmpty(licenseText))
            {
                throw new Exception("Empty license string");
            }

            SignedXmlVerifier.VerifyXml(licenseText);
        }

        const string Modulus = "5M9/p7N+JczIN/e5eObahxeCIe//2xRLA9YTam7zBrcUGt1UlnXqL0l/8uO8rsO5tl+tjjIV9bOTpDLfx0H03VJyxsE8BEpSVu48xujvI25+0mWRnk4V50bDZykCTS3Du0c8XvYj5jIKOHPtU//mKXVULhagT8GkAnNnMj9CvTc=";
        const string Exponent = "AQAB";

        public const string PublicKey = "<RSAKeyValue><Modulus>" + Modulus + "</Modulus><Exponent>" + Exponent + "</Exponent></RSAKeyValue>";

        static class SignedXmlVerifier
        {
            public static void VerifyXml(string xml)
            {
                var doc = LoadXmlDoc(xml);

                using (var rsa = new RSACryptoServiceProvider())
                {
                    var parameters = new RSAParameters
                    {
                        Modulus = Convert.FromBase64String(Modulus),
                        Exponent = Convert.FromBase64String(Exponent)
                    };

                    rsa.ImportParameters(parameters);

                    var nsMgr = new XmlNamespaceManager(doc.NameTable);
                    nsMgr.AddNamespace("sig", "http://www.w3.org/2000/09/xmldsig#");

                    var signedXml = new SignedXml(doc);
                    var signature = (XmlElement)doc.SelectSingleNode("//sig:Signature", nsMgr);
                    if (signature == null)
                    {
                        throw new Exception("Xml is invalid as it has no XML signature");
                    }
                    signedXml.LoadXml(signature);

                    if (!signedXml.CheckSignature(rsa))
                    {
                        throw new Exception("Xml is invalid as it failed signature check.");
                    }
                }
            }

            static XmlDocument LoadXmlDoc(string xml)
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(xml);
                    return doc;
                }
                catch (XmlException exception)
                {
                    throw new Exception("The text provided could not be parsed as XML.", exception);
                }
            }
        }
    }
}