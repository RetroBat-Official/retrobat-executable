using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace RetroBat
{
    class RegistryTools
    {
        public static void SetRegistryKey(string appFolder)
        {
            SimpleLogger.Instance.Info("Writing values to registry.");

            string registryPath = @"SOFTWARE\RetroBat";
            string ftpPath = "InstallRootUrl";
            string ftpPathNew = "InstallRootUrlNew";
            string installPath = "LatestKnownInstallPath";

            string urlValue = "http://www.retrobat.ovh/repo/win64";
            string urlValueNew = "http://www.retrobat.org/repo/win64";

            using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(registryPath))
            {
                if (registryKey.GetValue(ftpPath) == null)
                    registryKey.SetValue(ftpPath, urlValue);
                else
                {
                    string currentFTPPath = registryKey.GetValue(ftpPath).ToString();
                    if (currentFTPPath != urlValue)
                        registryKey.SetValue(ftpPath, urlValue);
                }

                if (registryKey.GetValue(ftpPathNew) == null)
                    registryKey.SetValue(ftpPathNew, urlValueNew);
                else
                {
                    string currentFTPPathNew = registryKey.GetValue(ftpPathNew).ToString();
                    if (currentFTPPathNew != urlValueNew)
                        registryKey.SetValue(ftpPathNew, urlValueNew);
                }

                if (registryKey.GetValue(installPath) == null)
                    registryKey.SetValue(installPath, appFolder);
                else
                {
                    string currentInstallPath = registryKey.GetValue(installPath).ToString();
                    if (currentInstallPath != appFolder)
                        registryKey.SetValue(installPath, appFolder);
                }
            }
        }
    }
}
