﻿using Darkages.Network.Game;
using Darkages.Network.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using Darkages.Scripting;
using Newtonsoft.Json;

namespace Darkages.Types
{

    public interface IEphermeral
    {
        void UpdateSpawns(TimeSpan elapsedTime);
        void Spawn(string creatureName, string script, double lifespan = 120, double updateRate = 650);
        void Despawn();
    }


    public abstract class Summon : ObjectManager, IEphermeral
    {
        [JsonIgnore]
        private GameClient _client;

        public GameServerTimer ObjectsUpdateTimer { get; set; }
        public GameServerTimer ObjectsRemovedTimer { get; set; }

        [JsonIgnore]
        public List<(string, Monster)> Spawns = new List<(string, Monster)>();

        public Template Template { get; set; }
        public string Script { get; set; }

        protected Summon(GameClient client)
        {
            _client = client;
        }

        public void Spawn(string creatureName, string script, double lifespan = 120, double updateRate = 650)
        {
            ObjectsRemovedTimer = new GameServerTimer(TimeSpan.FromSeconds(lifespan)); 
            ObjectsUpdateTimer = new GameServerTimer(TimeSpan.FromMilliseconds(updateRate));

            Template = ServerContext.GlobalMonsterTemplateCache.FirstOrDefault(i => i.BaseName == creatureName);
            Script = script;

            CreateLocal();
        }

        private void CreateLocal()
        {
            if (Template != null) 
                Create(Template, Script);
        }

        public void Despawn()
        {
            lock (Spawns)
            {
                foreach (var (_, spawn) in Spawns)
                {
                    spawn.Remove();
                }

                Spawns.Clear();
            }
        }

        public virtual void Update(TimeSpan elapsedTime)
        {
            if (ObjectsRemovedTimer != null && ObjectsRemovedTimer.Update(elapsedTime))
            {
                Despawn();
            }

            if (ObjectsUpdateTimer != null && ObjectsUpdateTimer.Update(elapsedTime))
            {
                UpdateSpawns(elapsedTime);
            }

        }

        private void Create(Template template, string script, int count = 1)
        {
            if (_client == null)
                return;

            switch (template)
            {
                case MonsterTemplate monsterTemplate:
                {
                    for (var i = 0; i < count; i++)
                    {
                        // we are changing this on the fly, we don't commit any changes to the original monster template.
                        monsterTemplate.IgnoreCollision = true;

                        var monster = Monster.Create(monsterTemplate, _client.Aisling.Map);

                        monster.Summoner = _client.Aisling;
                        monster.X = _client.Aisling.LastPosition.X;
                        monster.Y = _client.Aisling.LastPosition.Y;
                        monster.CurrentMapId = _client.Aisling.CurrentMapId;
                        monster.Scripts = ScriptManager.Load<MonsterScript>(script, monster, monster.Map);

                        lock (Spawns)
                        {
                            Spawns.Add((_client.Aisling.Username, monster));
                        }

                        AddObject(monster);
                    }

                    break;
                }
            }
        }

        public abstract void UpdateSpawns(TimeSpan elapsedTime);
    }
}
