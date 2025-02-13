﻿using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TapSDK.Core;
using UnityEngine;

namespace TapSDK.Compliance.Internal 
{
    /// <summary>
    /// 通用 JSON 序列化工具
    /// </summary>
    internal class Persistence 
    {
        private readonly string _filePath;
        internal Persistence(string path) 
        {
            string new_cacheDir = Path.Combine(Application.persistentDataPath, Config.ANTI_ADDICTION_DIR);
            if (!string.IsNullOrEmpty(TapTapComplianceManager.ClientId)){
                new_cacheDir = Path.Combine(Application.persistentDataPath, Config.ANTI_ADDICTION_DIR + "_" + TapTapComplianceManager.ClientId);
            }
            // 文件夹不存在时，尝试兼容旧版本数据
            if(!Directory.Exists(new_cacheDir)) {
                string old_cacheDir = Path.Combine(Application.persistentDataPath, Config.ANTI_ADDICTION_DIR);
                if(Directory.Exists(old_cacheDir)) {
                    Directory.Move(old_cacheDir, new_cacheDir);
                }
            }
            _filePath = Path.Combine(new_cacheDir, path);
        }

        internal async Task<T> Load<T>() where T : class 
        {
            TapLogger.Debug(_filePath);
            if (!File.Exists(_filePath)) 
            {
                return null;  
            }

            string text;
            using (FileStream fs = File.OpenRead(_filePath)) 
            {
                byte[] buffer = new byte[fs.Length];
                await fs.ReadAsync(buffer, 0, (int)fs.Length);
                text = Encoding.UTF8.GetString(buffer);
            }
            try 
            {
                return JsonConvert.DeserializeObject<T>(text);
            } 
            catch (Exception e) 
            {
                TapLogger.Error(e);
                Delete();
                return null;
            }
        }

        internal async Task Save<T>(T obj) 
        {
            if (obj == null) 
            {
                TapLogger.Error("Saved object is null.");
                return;
            }

            string text;
            try 
            {
                text = JsonConvert.SerializeObject(obj);
            } 
            catch (Exception e) 
            {
                TapLogger.Error(e);
                return;
            }

            string dirPath = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath)) 
            {
                Directory.CreateDirectory(dirPath);
            }

            using (FileStream fs = File.Create(_filePath))
            {
                byte[] buffer = Encoding.UTF8.GetBytes(text);
                await fs.WriteAsync(buffer, 0, buffer.Length);
            }
        }

        internal void Delete() 
        {
            if (!File.Exists(_filePath)) 
            {
                return;
            }

            File.Delete(_filePath);
        }
    }
}
