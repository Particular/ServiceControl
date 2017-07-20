namespace ServiceControlInstaller.CustomActions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Deployment.WindowsInstaller;
    using Microsoft.Win32;
    using ServiceControlInstaller.CustomActions.Extensions;
    using ServiceControlInstaller.Engine;

    // We need to remove any reference to the previous MSI and below
    // We can't simply upgrade since this would clobber the existing service
    public class CustomActionsMigrations
    {
        static readonly Guid UpgradeCode = new Guid("{8F59B8C7-A3EB-4D19-9B88-0EA69FF1B26C}");

        static readonly Dictionary<string,string> oldProducts = new Dictionary<string, string>
        {
                {"1.0.0", "{12BDB3E7-3A70-410A-A2C0-43037E33E3E6}"},
                {"1.1.0", "{284D3F83-82F3-4A17-A8AA-F2CBF0298152}"},
                {"1.2.0", "{988C86F3-B5CB-4F94-BD38-D8B62C7C998F}"},
                {"1.2.1", "{1BD45935-BB9D-464F-AC09-A824ED476BFA}"},
                {"1.2.2", "{6AF38546-DA28-4477-9A33-0B6FC759FB69}"},
                {"1.2.3", "{AC9365C3-7EF5-4CF5-A356-B5288CBF425F}"},
                {"1.3.0", "{7854D33C-307C-477A-850F-6D99AEBA4EF6}"},
                {"1.4.0", "{404D7A44-2767-4396-9C44-48738FAA59F1}"},
                {"1.4.1", "{BDEDFBEA-9FAB-488E-ACE3-939747E9B609}"},
                {"1.4.2", "{E18AF9FC-F4BE-4713-B6DC-A14A05BADA59}"},
                {"1.4.3", "{EF682A8F-9224-44DD-949F-D910D12A8C53}"},
                {"1.4.4", "{D23AF353-1F0E-4C0E-909A-166E500B9715}"},
                {"1.5.0", "{A4B5B1FE-8A11-4920-8741-3CA68F0A0136}"},
                {"1.5.1", "{78E3CB24-D00A-4CC6-A7C2-CDB66231B8DA}"},
                {"1.5.2", "{3AC40768-536F-4E2F-A2A4-E198E6FBA1E9}"},
                {"1.5.3", "{8EFE762B-399A-4EB7-9C5E-AAB59EB73F98}"},
                {"1.6.0", "{3260B961-C140-4BFC-9A54-583274405F43}"},
                {"1.6.1", "{FD225DAC-FE16-4634-9C80-C1F3533347B6}"},
                {"1.6.2", "{164571BF-F42F-4492-9047-25101B0ABB87}"},
                {"1.6.3", "{657CB451-3B6E-4266-90DF-F6C16A8AC2CE}"}
        };

        [CustomAction]
        public static ActionResult ServiceControlMigration(Session session)
        {
            var logger = new MSILogger(session);
            RemoveProductFromMSIList(logger);
            RemoveOrphanedInstallationKeys();
            return ActionResult.Success;
        }

        public static void RemoveProductFromMSIList(ILogging logger)
        {
            var upgradeKeyPath = $@"SOFTWARE\Classes\Installer\UpgradeCodes\{UpgradeCode.Flip():N}";
            using (var installerKey = Registry.LocalMachine.OpenSubKey(upgradeKeyPath, true))
            {
                if (installerKey == null)
                    return;

                logger.Info($"Found upgrade code for old version {UpgradeCode:B}");
                foreach (var flippedProductCodeString in installerKey.GetValueNames())
                {
                    Guid flippedProductCode;
                    if (!Guid.TryParse(flippedProductCodeString, out flippedProductCode))
                    {
                        continue;
                    }

                    var keysToDelete = new[]
                    {
                        $@"SOFTWARE\Classes\Installer\Products\{flippedProductCode:N}",
                        $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\{flippedProductCode:N}"
                    };
                    foreach (var regkey in keysToDelete)
                    {
                        bool keyExists;
                        using (var productKey = Registry.LocalMachine.OpenSubKey(regkey))
                        {
                            keyExists = (productKey != null);
                        }

                        if (keyExists)
                        {
                            logger.Info($@"Removing HKEY_LOCAL_MACHINE\{regkey}");
                            Registry.LocalMachine.DeleteSubKeyTree(regkey, false);
                        }
                    }

                    var productCode = flippedProductCode.Flip().ToString("B");

                    var uninstallKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{productCode}";
                    using (var wowRegistryRoot = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    {
                        var productKeyPresent = false;
                        using (var productKey = wowRegistryRoot.OpenSubKey(uninstallKey))
                        {
                            if (productKey != null)
                            {
                                productKeyPresent = true;
                                var displayName = (string) productKey.GetValue("DisplayName", String.Empty);
                                var displayVersion = (string)productKey.GetValue("DisplayVersion", String.Empty);

                                var descriptiveVersion = $"{displayName} {displayVersion}";

                                if (!string.IsNullOrWhiteSpace(descriptiveVersion))
                                {
                                    var descriptiveUninstallKeyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{descriptiveVersion}";
                                    bool descriptiveUninstallPresent;
                                    using (var descriptiveUninstallkey = wowRegistryRoot.OpenSubKey(descriptiveUninstallKeyPath))
                                    {
                                        descriptiveUninstallPresent = (descriptiveUninstallkey != null);
                                    }
                                    if (descriptiveUninstallPresent)
                                    {
                                        if (Environment.Is64BitOperatingSystem)
                                        {
                                            logger.Info($@"Removing HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{descriptiveVersion}");
                                        }
                                        else
                                        {
                                            logger.Info($@"Removing HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{descriptiveVersion}");
                                        }
                                        logger.Info($@"Removing HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{descriptiveVersion}");
                                        wowRegistryRoot.DeleteSubKeyTree(descriptiveUninstallKeyPath);
                                    }
                                }
                            }
                        }
                        if (productKeyPresent)
                        {
                            if (Environment.Is64BitOperatingSystem)
                            {
                                logger.Info($@"Removing HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{productCode}");
                            }
                            else
                            {
                                logger.Info($@"Removing HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{productCode}");
                            }
                            Registry.LocalMachine.DeleteSubKeyTree(upgradeKeyPath, false);
                        }
                    }
                }
            }
            logger.Info($@"Removing HKEY_LOCAL_MACHINE\{upgradeKeyPath}");
            Registry.LocalMachine.DeleteSubKeyTree(upgradeKeyPath, false);
        }

        static void RemoveOrphanedInstallationKeys()
        {
            using (var wowRegistryRoot = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                foreach (var uninstallKey in oldProducts.Select(product => $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{product.Value}"))
                {
                    wowRegistryRoot.DeleteSubKeyTree(uninstallKey, false);
                }
            }

            foreach (var uninstallKey in oldProducts.Select(product => $@"SOFTWARE\Classes\Installer\Products\{new Guid(product.Value).Flip():N}"))
            {
                Registry.LocalMachine.DeleteSubKey(uninstallKey, false);
            }
        }
    }
}
