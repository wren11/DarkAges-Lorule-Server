﻿///************************************************************************
//Project Lorule: A Dark Ages Client (http://darkages.creatorlink.net/index/)
//Copyright(C) 2018 TrippyInc Pty Ltd
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.If not, see<http://www.gnu.org/licenses/>.
//*************************************************************************/

using System.IO;
using Darkages.Types;
using Newtonsoft.Json;

namespace Darkages.Storage
{
    public class StorageManager
    {
        public static JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            Formatting = Formatting.Indented
        };

        public static AislingStorage AislingBucket = new AislingStorage();
        public static AreaStorage AreaBucket = new AreaStorage();
        public static WarpStorage WarpBucket = new WarpStorage();

        public static TemplateStorage<SkillTemplate> SkillBucket = new TemplateStorage<SkillTemplate>();
        public static TemplateStorage<SpellTemplate> SpellBucket = new TemplateStorage<SpellTemplate>();
        public static TemplateStorage<ItemTemplate> ItemBucket = new TemplateStorage<ItemTemplate>();
        public static TemplateStorage<MonsterTemplate> MonsterBucket = new TemplateStorage<MonsterTemplate>();
        public static TemplateStorage<MundaneTemplate> MundaneBucket = new TemplateStorage<MundaneTemplate>();
        public static TemplateStorage<WorldMapTemplate> WorldMapBucket = new TemplateStorage<WorldMapTemplate>();
        public static TemplateStorage<PopupTemplate> PopupBucket = new TemplateStorage<PopupTemplate>();
        public static TemplateStorage<Reactor> ReactorBucket = new TemplateStorage<Reactor>();

        static StorageManager()
        {
        }

        public void SaveSorageContainers()
        {
            if (ServerContextBase.Paused)
                return;
            foreach (var item in ServerContextBase.GlobalItemTemplateCache.Values)
            {
                if (ItemBucket.IsStored(item)) continue;

                ItemBucket.Save(item);
            }
        }

        public static T Load<T>() where T : class, new()
        {
            try
            {
                var obj = new T();

                if (obj is ServerConstants)
                {
                    var StoragePath = $@"{ServerContextBase.StoragePath}\lorule_config";
                    var path = Path.Combine(StoragePath, $"{"global"}.json");

                    if (!File.Exists(path))
                        return null;

                    T result;

                    using (var s = File.OpenRead(path))
                    using (var f = new StreamReader(s))
                    {
                        result = JsonConvert.DeserializeObject<ServerConstants>(f.ReadToEnd(), Settings) as T;
                    }

                    return result;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public static string Save<T>(T obj)
        {
            try
            {
                if (obj is ServerConstants)
                {
                    var StoragePath = $@"{ServerContextBase.StoragePath}\lorule_config";

                    if (!Directory.Exists(StoragePath))
                        Directory.CreateDirectory(StoragePath);

                    var path = Path.Combine(StoragePath, $"{"global"}.json");
                    var objString = JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    });

                    File.WriteAllText(path, objString);
                    return objString;
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}