﻿using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using IpBlocker.Core.Interfaces;
using MaxMind.GeoIP2;
using System;
using System.IO;
using System.Linq;
using System.Net;

namespace IpBlocker.IpLocation.Maxmind
{
    public class MaxmindIpLocation : IIPLocator, IDisposable
    {
        private string _tempPath;
        private string _maxMindDbFilePath;
        private string _dataPath;
        private DatabaseReader _reader;
        private object _dbLock = new object();

        private const string MaxMindDbUrl = "https://geolite.maxmind.com/download/geoip/database/GeoLite2-Country.tar.gz";
        private const string TarFileName = "GeoLite2-Country.tar.gz";
        private const string DbFileName = "GeoLite2-Country.mmdb";

        public MaxmindIpLocation()
        {
            _tempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

            _maxMindDbFilePath = Path.Combine(_dataPath, DbFileName);
        }

        public string GetIpLocation(string Ip)
        {
            lock (_dbLock)
            {
                OpenReader();

                try
                {
                    var response = _reader.Country(Ip);
                    Console.WriteLine(response.Country);
                    Console.WriteLine(response.Continent);

                    return $"{response.Country.Name}, {response?.Continent?.Name ?? "Unknown" }";
                }
                catch (Exception)
                {
                    return "Unknown";
                }
            }
        }

        private void OpenReader()
        {
            if (_reader == null)
            {
                _reader = new DatabaseReader(_maxMindDbFilePath);
            }
        }

        private void CloseReader()
        {
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }
        }

        public void Initialize()
        {
            if (File.Exists(_maxMindDbFilePath))
            {
                return;
            }

            CreateDir(_tempPath, true);
            CreateDir(_dataPath, false);

            var downloadedFile = Path.Combine(_tempPath, TarFileName);

            using (var client = new WebClient())
            {
                client.DownloadFile(MaxMindDbUrl, Path.Combine(_tempPath, TarFileName));
            }

            ExtractFile(downloadedFile);
        }

        private void ExtractFile(string zipFileName)
        {
            var tarFile = ExtractGZipFile(zipFileName);

            var dataFile = ExtractTarFile(tarFile);

            if (dataFile != null)
            {
                lock (_dbLock)
                {
                    CloseReader();
                    File.Copy(dataFile, _maxMindDbFilePath, true);
                }
            }
        }

        private string ExtractGZipFile(string zipFileName)
        {
            string tarName;
            byte[] dataBuffer = new byte[4096];
            using (var originalFileStream = new FileStream(zipFileName, FileMode.Open, FileAccess.Read))
            {
                using (var gzipStream = new GZipInputStream(originalFileStream))
                {
                    // Change this to your needs
                    tarName = Path.Combine(_tempPath, Path.GetFileNameWithoutExtension(TarFileName));

                    using (var tarFileStream = File.Create(tarName))
                    {
                        StreamUtils.Copy(gzipStream, tarFileStream, dataBuffer);
                    }
                }
            }

            return tarName;
        }

        public string ExtractTarFile(string tarFileName)
        {
            using (Stream inStream = File.OpenRead(tarFileName))
            {
                using (var tarArchive = TarArchive.CreateInputTarArchive(inStream))
                {
                    tarArchive.ExtractContents(_tempPath);
                }
            }

            foreach (var directory in Directory.GetDirectories(_tempPath))
            {
                var files = Directory.GetFiles(directory);

                if (files != null && files.Any(f => f.EndsWith(".mmdb")))
                {
                    return files.First(f => f.EndsWith(".mmdb"));
                }
            }

            return null;
        }

        private void CreateDir(string path, bool deleteExists)
        {
            if (deleteExists && Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public void Dispose()
        {
            CloseReader();
        }
    }
}