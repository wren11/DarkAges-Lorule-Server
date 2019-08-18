﻿///************************************************************************
//Project Lorule: A Dark Ages Server (http://darkages.creatorlink.net/index/)
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Darkages.Common;
using Darkages.Network;
using Darkages.Network.Game;
using Darkages.Network.Object;
using Darkages.Network.ServerFormats;
using LiteDB;
using Newtonsoft.Json;
using static Darkages.Types.ElementManager;

namespace Darkages.Types
{
    public abstract class Sprite : ObjectManager, INotifyPropertyChanged
    {
        private readonly Random _rnd = new Random();

        [JsonIgnore]
        [BsonIgnore]
        public Random rnd
        {
            get => _rnd;
        }


        [JsonIgnore] [BsonIgnore] public byte LastDirection;

        [JsonIgnore] [BsonIgnore] public Position LastPosition;


        #region Sprite Constructor

        public Sprite()
        {
            if (this is Aisling)
                EntityType = TileContent.Aisling;
            if (this is Monster)
                EntityType = TileContent.Monster;
            if (this is Mundane)
                EntityType = TileContent.Mundane;
            if (this is Money)
                EntityType = TileContent.None;
            if (this is Item)
                EntityType = TileContent.None;

            Amplified = 0;
            Target = null;


            Buffs = new ConcurrentDictionary<string, Buff>();
            Debuffs = new ConcurrentDictionary<string, Debuff>();

            LastTargetAcquired = DateTime.UtcNow;
            LastMovementChanged = DateTime.UtcNow;
            LastUpdated = DateTime.UtcNow;
            LastPosition = new Position(0, 0);
            LastDirection = 0;
        }

        #endregion


        [JsonIgnore] [BsonIgnore] public GameClient Client { get; set; }

        [JsonIgnore]
        [BsonIgnore]
        public Area Map
        {
            get
            {

                if (!InsideInstance)
                {
                    return ServerContext.GlobalMapCache.ContainsKey(CurrentMapId) ? ServerContext.GlobalMapCache[CurrentMapId] ?? null : null;
                }
                else
                {
                    if (Instance != null)
                    {
                        ServerContext.logger.Debug("Map {0} Instance: Unique", CurrentMapId);
                        return Instance;
                    }
                }

                return null;
            }
        }

        [JsonProperty]
        public Area Instance { get; set; }

        [JsonIgnore] [BsonIgnore] public TileContent EntityType { get; set; }

        [JsonIgnore] [BsonIgnore] public Sprite Target { get; set; }

        [JsonIgnore] [BsonIgnore] public Position Position => new Position(XPos, YPos);

        [JsonIgnore] [BsonIgnore] public bool Attackable => this is Monster || this is Aisling || this is Mundane;

        [JsonIgnore] [BsonIgnore] public bool Alive => CurrentHp > 0;

        [JsonIgnore] [BsonIgnore] public DateTime AbandonedDate { get; set; }

        [JsonIgnore] [BsonIgnore] public DateTime LastUpdated { get; set; }

        [JsonIgnore] [BsonIgnore] public DateTime LastTargetAcquired { get; set; }

        [JsonIgnore] [BsonIgnore] public DateTime LastMovementChanged { get; set; }

        [JsonIgnore] [BsonIgnore]
        public int Level => EntityType == TileContent.Aisling ? (this as Aisling).ExpLevel
            : EntityType == TileContent.Monster ? (this as Monster).Template.Level
            : EntityType == TileContent.Mundane ? (this as Mundane).Template.Level
            : EntityType == TileContent.Item ? (this as Item).Template.LevelRequired : 0;

        public ConcurrentDictionary<string, Debuff> Debuffs { get; set; }

        public ConcurrentDictionary<string, Buff> Buffs { get; set; }


        public Element OffenseElement { get; set; }

        public Element DefenseElement { get; set; }

        public PrimaryStat MajorAttribute { get; set; }

        public byte Direction { get; set; }

        public int CurrentMapId { get; set; }

        public int Amplified { get; set; }


        [JsonIgnore] [BsonIgnore] public bool CanMove => !(IsFrozen || IsSleeping || IsParalyzed);

        [JsonIgnore] [BsonIgnore] public bool CanCast => !(IsFrozen || IsSleeping);

        [JsonIgnore] [BsonIgnore] public bool EmpoweredAssail { get; set; }

        public bool Immunity { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool InsideInstance { get; set; } = false;

        #region Identification & Position

        [BsonId]
        public int Serial { get; set; }

        public int X;

        public int Y;

        [JsonIgnore] [BsonIgnore]
        public int XPos
        {
            get => X;
            set
            {
                if (X != value)
                {
                    X = value;
                    NotifyPropertyChanged();
                }
            }
        }

        [JsonIgnore] [BsonIgnore]
        public int YPos
        {
            get => Y;
            set
            {
                if (Y != value)
                {
                    Y = value;
                    NotifyPropertyChanged();
                }
            }
        }

        #endregion

        #region Attributes

        public int CurrentHp { get; set; }

        public int CurrentMp { get; set; }

        public int _MaximumHp { get; set; }

        public int _MaximumMp { get; set; }

        public byte _Str { get; set; }

        public byte _Int { get; set; }

        public byte _Wis { get; set; }

        public byte _Con { get; set; }

        public byte _Dex { get; set; }

        public byte _Mr { get; set; }

        public byte _Dmg { get; set; }

        public byte _Hit { get; set; }

        public int _Regen { get; set; }

        [JsonIgnore] [BsonIgnore] public int Regen => (_Regen + BonusRegen).Clamp(0, 300);

        [JsonIgnore] [BsonIgnore] public int MaximumHp => _MaximumHp + BonusHp;

        [JsonIgnore] [BsonIgnore] public int MaximumMp => _MaximumMp + BonusMp;

        [JsonIgnore] [BsonIgnore]
        public byte Str
        {
            get
            {
                var tmp = (byte) (_Str + BonusStr).Clamp(1, byte.MaxValue);
                if (tmp > 255)
                    return 255;

                return tmp;
            }
        }

        [JsonIgnore] [BsonIgnore]
        public byte Int
        {
            get
            {
                var tmp = (byte) (_Int + BonusInt).Clamp(1, byte.MaxValue);
                if (tmp > 255)
                    return 255;

                return tmp;
            }
        }

        [JsonIgnore] [BsonIgnore]
        public byte Wis
        {
            get
            {
                var tmp = (byte) (_Wis + BonusWis).Clamp(1, byte.MaxValue);
                if (tmp > 255)
                    return 255;

                return tmp;
            }
        }

        [JsonIgnore] [BsonIgnore]
        public byte Con
        {
            get
            {
                var tmp = (byte) (_Con + BonusCon).Clamp(1, byte.MaxValue);
                if (tmp > 255)
                    return 255;

                return tmp;
            }
        }

        [JsonIgnore] [BsonIgnore]
        public byte Dex
        {
            get
            {
                var tmp = (byte) (_Dex + BonusDex).Clamp(1, byte.MaxValue);
                if (tmp > 255)
                    return 255;

                return tmp;
            }
        }

        [JsonIgnore] [BsonIgnore]
        public int Ac
        {
            get
            {
                if (BonusAc < -70)
                    return -70;

                return BonusAc;
            }
        }


        [JsonIgnore] [BsonIgnore] public byte Mr => (byte) (_Mr + BonusMr).Clamp(0, 70);

        [JsonIgnore] [BsonIgnore] public byte Dmg => (byte) (_Dmg + BonusDmg).Clamp(0, byte.MaxValue);

        [JsonIgnore] [BsonIgnore] public byte Hit => (byte) (_Hit + BonusHit).Clamp(0, byte.MaxValue);

        [JsonIgnore] [BsonIgnore] public int BonusStr { get; set; }

        [JsonIgnore] [BsonIgnore] public int BonusInt { get; set; }

        [JsonIgnore] [BsonIgnore] public int BonusWis { get; set; }

        [JsonIgnore] [BsonIgnore] public int BonusCon { get; set; }

        [JsonIgnore] [BsonIgnore] public int BonusDex { get; set; }

        [JsonIgnore] [BsonIgnore] public byte BonusMr { get; set; }

        [JsonIgnore] [BsonIgnore] public int BonusAc { get; set; }

        [JsonIgnore] [BsonIgnore] public byte BonusHit { get; set; }

        [JsonIgnore] [BsonIgnore] public byte BonusDmg { get; set; }

        [JsonIgnore] [BsonIgnore] public int BonusHp { get; set; }

        [JsonIgnore] [BsonIgnore] public int BonusMp { get; set; }

        [JsonIgnore] [BsonIgnore] public int BonusRegen { get; set; }

        #endregion

        #region Status

        [JsonIgnore] [BsonIgnore] public bool IsAited => HasBuff("aite");

        [JsonIgnore] [BsonIgnore] public bool IsSleeping => HasDebuff("sleep");

        [JsonIgnore] [BsonIgnore] public bool IsFrozen => HasDebuff("frozen");

        [JsonIgnore] [BsonIgnore] public bool IsPoisoned => HasDebuff(i => i.Name.ToLower().Contains("puinsein"));

        [JsonIgnore] [BsonIgnore] public bool IsCursed => HasDebuff(i => i.Name.ToLower().Contains("cradh"));

        [JsonIgnore] [BsonIgnore] public bool IsBleeding => HasDebuff("bleeding");

        [JsonIgnore] [BsonIgnore] public bool IsBlind => HasDebuff("blind");

        [JsonIgnore] [BsonIgnore] public bool IsConfused => HasDebuff("confused");

        [JsonIgnore] [BsonIgnore]
        public bool IsParalyzed => HasDebuff("paralyze") || HasDebuff(i => i.Name.ToLower().Contains("beag suain"));

        [JsonIgnore] [BsonIgnore]
        public int[][] Directions { get; } =
        {
            new[] {+0, -1},
            new[] {+1, +0},
            new[] {+0, +1},
            new[] {-1, +0}
        };

        [JsonIgnore] [BsonIgnore]
        public int[][] DirectionTable { get; } =
        {
            new[] {-1, +3, -1},
            new[] {+0, -1, +2},
            new[] {-1, +1, -1}
        };

        [JsonIgnore] [BsonIgnore] public bool Exists => GetObject(Map, i => i.Serial == Serial, Get.All) != null;

        #endregion

        public TSprite Cast<TSprite>()
            where TSprite : Sprite
        {
            return this as TSprite;
        }

        #region Sprite Methods

        public bool TrapsAreNearby()
        {
            return Trap.Traps.Select(i => i.Value).Any(i => i.CurrentMapId == CurrentMapId);
        }

        public bool TriggerNearbyTraps()
        {
            var trap = Trap.Traps.Select(i => i.Value).FirstOrDefault(i => i.Owner.Serial != Serial
                                                                           && Position.DistanceFrom(i.Location) <=
                                                                           i.Radius);

            if (trap != null)
                Trap.Activate(trap, this);

            return false;
        }

        public bool CanHitTarget(Sprite target)
        {
            return true;
        }

        public bool HasBuff(string buff)
        {
            if (Buffs == null || Buffs.Count == 0)
                return false;

            return Buffs.ContainsKey(buff);
        }

        public bool HasDebuff(string debuff)
        {
            if (Debuffs == null || Debuffs.Count == 0)
                return false;

            return Debuffs.ContainsKey(debuff);
        }

        public bool HasDebuff(Func<Debuff, bool> p)
        {
            if (Debuffs == null || Debuffs.Count == 0)
                return false;

            return Debuffs.Select(i => i.Value).FirstOrDefault(p) != null;
        }

        public string GetDebuffName(Func<Debuff, bool> p)
        {
            if (Debuffs == null || Debuffs.Count == 0)
                return string.Empty;

            return Debuffs.Select(i => i.Value)
                .FirstOrDefault(p)
                ?.Name;
        }

        public bool RemoveBuff(string buff)
        {
            if (HasBuff(buff))
            {
                var buffobj = Buffs[buff];
                buffobj?.OnEnded(this, buffobj);

                return true;
            }

            return false;
        }

        public bool RemoveDebuff(string debuff, bool cancelled = false)
        {
            if (!cancelled && debuff == "skulled")
                return true;

            if (HasDebuff(debuff))
            {
                var buffobj = Debuffs[debuff];

                if (buffobj != null)
                {
                    buffobj.Cancelled = cancelled;
                    buffobj.OnEnded(this, buffobj);
                    return true;
                }
            }

            return false;
        }

        public int GetBaseDamage(Sprite target, MonsterDamageType type)
        {
            if (this is Monster || this is Mundane)
            {
                var mod  = 0.0;
                var diff = 0;

                if (target is Aisling obj)
                    diff = Level + 1 - obj.ExpLevel;

                if (target is Monster tmon)
                    diff = Level + 1 - tmon.Template.Level;

                if (diff <= 0)
                    mod = Level * (type == MonsterDamageType.Physical ? 0.1 : 2) * 60;
                else
                    mod = Level * (type == MonsterDamageType.Physical ? 0.1 : 2) * (60 * diff);


                var dmg = Math.Abs((int) (mod + 1));

                if (dmg <= 0)
                    dmg = 1;

                return dmg;
            }

            return 1;
        }

        public void RemoveAllBuffs()
        {
            if (Buffs != null)
                foreach (var buff in Buffs)
                    RemoveBuff(buff.Key);
        }

        public void RemoveAllDebuffs()
        {
            if (Debuffs != null)
                foreach (var debuff in Debuffs)
                    RemoveDebuff(debuff.Key);
        }

        public void RemoveBuffsAndDebuffs()
        {
            RemoveAllBuffs();
            RemoveAllDebuffs();
        }

        public void ApplyDamage(Sprite source, int dmg, Element element, byte sound = 1)
        {
            element = CheckRandomElement(element);

            var saved = source.OffenseElement;
            {
                source.OffenseElement = element;
                ApplyDamage(source, dmg, false, sound);
                source.OffenseElement = saved;
            }
        }

        public static Element CheckRandomElement(Element element)
        {
            if (element == Element.Random)
                element = Generator.RandomEnumValue<Element>();

            return element;
        }

        public void ApplyDamage(Sprite source, int dmg,  bool penetrating = false, byte sound = 1, Action<int> dmgcb = null, bool forceTarget = false)
        {
            if (!WithinRangeOf(source))
                return;

            if (!Attackable)
                return;

            if (!CanBeAttackedHere(source))
                return;

            if (CannotTagTarget(source, forceTarget))
                return;

            dmg = ApplyWeaponBonuses(source, dmg);

            if (dmg > 0)
                ApplyEquipmentDurability(dmg);

            if (!DamageTarget(ref dmg, penetrating, sound, dmgcb))
                return;

            OnDamaged(source, dmg);
        }

        private void OnDamaged(Sprite source, int dmg)
        {
            (this as Aisling)?.Client.SendStats(StatusFlags.StructB);
            (source as Aisling)?.Client.SendStats(StatusFlags.StructB);

            if (!(this is Monster))
                return;

            if (source is Aisling aisling)
                (this as Monster)?.Script?.OnDamaged(aisling?.Client, dmg);
        }

        private bool DamageTarget(ref int dmg, bool penetrating, byte sound, Action<int> dmgcb)
        {
            if (penetrating)
            {
                var empty = new ServerFormat13
                {
                    Serial = Serial,
                    Health = byte.MaxValue,
                    Sound = sound
                };

                Show(Scope.VeryNearbyAislings, empty);

                CurrentHp -= dmg;

                if (CurrentHp < 0)
                    CurrentHp = 0;
            }
            else
            {
                if (Immunity)
                {
                    var empty = new ServerFormat13
                    {
                        Serial = Serial,
                        Health = byte.MaxValue,
                        Sound = sound
                    };

                    Show(Scope.VeryNearbyAislings, empty);
                    return false;
                }

                if (HasDebuff("sleep"))
                    dmg <<= 1;

                RemoveDebuff("sleep");

                if (IsAited && dmg > 5)
                    dmg /= 3;

                var amplifier = GetElementalModifier(Target);
                {
                    dmg = ComputeDmgFromAc(dmg, Target);
                    dmg = CompleteDamageApplication(dmg, sound, dmgcb, amplifier);
                }
            }

            return true;
        }

        private bool CannotTagTarget(Sprite source, bool forceTarget)
        {
            if (!CanAcceptTarget(source))
            {
                if (source is Aisling aisling)
                    aisling.Client?.SendMessage(0x02, ServerContext.Config.CantAttack);

                if (!forceTarget)
                    return true;
            }

            if (forceTarget)
            {
                Target = source;
            }
            else
            {
                if (Target == null)
                    Target = source;
            }

            if (Target == null)
                return true;

            if (!(this is Monster monster))
                return false;

            monster.AppendTags(source);
            monster.Script?.OnAttacked(source?.Client);

            return false;
        }

        private int ApplyWeaponBonuses(Sprite source, int dmg)
        {
            if (source is Aisling aisling)
            {
                if (aisling.EquipmentManager.Weapon?.Item != null && aisling.Weapon > 0)
                {
                    var weapon = aisling.EquipmentManager.Weapon.Item;

                    lock (rnd)
                    {
                        dmg += rnd.Next(
                                   weapon.Template.DmgMin + 1, 
                                   weapon.Template.DmgMax + 5) + aisling.BonusDmg * 10 / 100;
                    }
                }
            }

            return dmg;
        }

        private void ApplyEquipmentDurability(int dmg)
        {
            if (this is Aisling aisling && aisling.DamageCounter++ % 2 == 0 && dmg > 0)
                aisling.EquipmentManager.DecreaseDurability();
        }

        private double GetElementalModifier(Sprite source)
        {
            if (source == null)
                return 1;

            var amplifier = 1.00;

            if (source.OffenseElement != Element.None)
            {
                var element = CheckRandomElement(source.OffenseElement);

                amplifier  = CalcaluteElementalAmplifier(element);

                amplifier *=
                    Amplified == 1 ? ServerContext.Config.FasNadurStrength + 10 :
                    Amplified == 2 ? ServerContext.Config.MorFasNadurStrength + 30 : 1.00;

                if (element == Element.None && DefenseElement != Element.None)
                    amplifier = 0.25;

                switch (DefenseElement)
                {
                    case Element.None when element != Element.None:
                        return 5.75;
                    case Element.None when element == Element.None:
                        return 0.25;
                    default:
                        return amplifier;
                }
            }

            return 0.20;
        }

        public bool CanAcceptTarget(Sprite source)
        {
            if (source == null ||
                !WithinRangeOf(source) ||
                !source.WithinRangeOf(this))
                return false;

            if (this is Monster)
                if (source is Aisling)
                {
                    var monster = this as Monster;
                    var aisling = source as Aisling;

                    if (monster.TaggedAislings.Count > 0)
                    {
                        var taggedalready = false;
                        foreach (var obj in monster.TaggedAislings)
                            //check if the user attacking is in the tagged list.
                            if (obj.Key == source.Serial)
                            {
                                taggedalready = true;
                                break;
                            }

                        //monster has been attacked by this user before.
                        if (taggedalready) return true;

                        //check if any tagged users are in the same group as this user.
                        foreach (var tagg in monster.TaggedAislings)
                            if (tagg.Value is Aisling obj)
                                if (obj.GroupParty.Has(aisling)
                                    && obj.WithinRangeOf(aisling)
                                    && monster.WithinRangeOf(monster))
                                    return true;

                        var abandoned = false;

                        //check if any tagged users are still near this monster or online.
                        foreach (var tagg in monster.TaggedAislings)
                            if (tagg.Value is Aisling obj)
                            {
                                if (!monster.WithinRangeOf(tagg.Value) || !(tagg.Value as Aisling).LoggedIn)
                                    abandoned = true;
                                else
                                    abandoned = false;
                            }

                        return !abandoned;
                    }

                    return true;
                }


            return true;
        }

        private double CalcaluteElementalAmplifier(Element element)
        {
            double damage_mod = 0.25;

            if (element == DefenseElement)
            {
                damage_mod = DefenseElement == Element.None && element != Element.None ? 1.75 : 0.25;

                return damage_mod;
            }

            switch (element)
            {
                //Fire -> Wind
                case Element.Fire:
                    damage_mod = DefenseElement == Element.Wind ? 1.75 : 0.25;

                    return damage_mod;
                //Wind -> Earth
                case Element.Wind:
                {
                    damage_mod = DefenseElement == Element.Earth ? 1.75 : 0.25;

                    return damage_mod;
                }

                //Water -> Fire
                case Element.Water:
                {
                    damage_mod = DefenseElement == Element.Fire ? 1.75 : 0.25;

                    return damage_mod;
                }

                //Earth -> Water
                case Element.Earth:
                {
                    damage_mod = DefenseElement == Element.Water ? 1.75 : 0.25;

                    return damage_mod;
                }

                //Dark -> All
                case Element.Dark:
                {
                    damage_mod = DefenseElement == Element.Light ? 2.75 : 0.25;

                    return damage_mod;
                }

                //Light -> All
                case Element.Light:
                    damage_mod = DefenseElement == Element.Light ? 2.75 : 0.25;
                    return damage_mod;
            }

            return damage_mod;
        }

        private int CompleteDamageApplication(int dmg, byte sound, Action<int> dmgcb, double amplifier)
        {
            if (dmg <= 0)
                dmg = 1;

            if (CurrentHp > MaximumHp)
                CurrentHp = MaximumHp;

            var dmg_applied = (int) Math.Abs(dmg * amplifier);

            CurrentHp -= dmg_applied;

            if (CurrentHp < 0)
                CurrentHp = 0;

            var hpbar = new ServerFormat13
            {
                Serial = Serial,
                Health = (ushort) ((double) 100 * CurrentHp / MaximumHp),
                Sound = sound
            };

            Show(Scope.VeryNearbyAislings, hpbar);
            {
                dmgcb?.Invoke(dmg_applied);
            }

            return dmg_applied;
        }

        /// <summary>
        ///     Checks the source of damage and if it's a player, check if the target is a player.
        ///     is true, checks weather or not damage can be applied on the map they are on both on.
        /// </summary>
        /// <param name="Source">Player applying damage.</param>
        /// <returns>true : false</returns>
        public bool CanBeAttackedHere(Sprite Source)
        {
            if (Source is Aisling && this is Aisling)
                if (CurrentMapId > 0 && ServerContext.GlobalMapCache.ContainsKey(CurrentMapId))
                    if (!ServerContext.GlobalMapCache[CurrentMapId].Flags.HasFlag(MapFlags.PlayerKill))
                        return false;

            return true;
        }

        /// <summary>
        ///     Sends Format With Target Scope.
        /// </summary>
        public void Show<T>(Scope op, T format, IEnumerable<Sprite> definer = null) where T : NetworkFormat
        {
            if (Map == null)
                return;


            try
            {
                switch (op)
                {
                    case Scope.Self:
                        Client?.Send(format);
                        break;
                    case Scope.NearbyAislingsExludingSelf:
                        foreach (var gc in GetObjects<Aisling>(Map, that => WithinRangeOf(that)))
                            if (gc.Serial != Serial)
                            {
                                if (this is Aisling)
                                {
                                    if (!gc.Client.Aisling.CanSeeHidden() && (this as Aisling).Invisible)
                                        if (format is ServerFormat33)
                                            return;

                                    if (!gc.Client.Aisling.CanSeeGhosts() && (this as Aisling).Dead)
                                        if (format is ServerFormat33)
                                            return;
                                }

                                gc.Client.Send(format);
                            }

                        break;
                    case Scope.NearbyAislings:
                        foreach (var gc in GetObjects<Aisling>(Map, that => WithinRangeOf(that)))
                        {
                            if (this is Aisling)
                            {
                                if (!gc.Client.Aisling.CanSeeHidden() && (this as Aisling).Invisible)
                                    if (format is ServerFormat33)
                                        return;

                                if (!gc.Client.Aisling.CanSeeGhosts() && (this as Aisling).Dead)
                                    if (format is ServerFormat33)
                                        return;
                            }

                            gc.Client.Send(format);
                        }

                        break;
                    case Scope.VeryNearbyAislings:
                        foreach (var gc in GetObjects<Aisling>(Map, that =>
                            WithinRangeOf(that, ServerContext.Config.VeryNearByProximity)))
                        {
                            if (this is Aisling)
                            {
                                if (!gc.Client.Aisling.CanSeeHidden() && (this as Aisling).Invisible)
                                    if (format is ServerFormat33)
                                        return;

                                if (!gc.Client.Aisling.CanSeeGhosts() && (this as Aisling).Dead)
                                    if (format is ServerFormat33)
                                        return;
                            }

                            gc.Client.Send(format);
                        }

                        break;
                    case Scope.AislingsOnSameMap:
                        foreach (var gc in GetObjects<Aisling>(Map, that => CurrentMapId == that.CurrentMapId))
                        {
                            if (this is Aisling)
                            {
                                if (!gc.Client.Aisling.CanSeeHidden() && (this as Aisling).Invisible)
                                    if (format is ServerFormat33)
                                        return;

                                if (!gc.Client.Aisling.CanSeeGhosts() && (this as Aisling).Dead)
                                    if (format is ServerFormat33)
                                        return;
                            }

                            gc.Client.Send(format);
                        }

                        break;
                    case Scope.GroupMembers:
                    {
                        if (this is Aisling)
                            foreach (var gc in GetObjects<Aisling>(Map, that => (this as Aisling).GroupParty.Has(that)))
                            {
                                if (!gc.Client.Aisling.CanSeeHidden() && (this as Aisling).Invisible)
                                    if (format is ServerFormat33)
                                        return;

                                if (!gc.Client.Aisling.CanSeeGhosts() && (this as Aisling).Dead)
                                    if (format is ServerFormat33)
                                        return;

                                gc.Client.Send(format);
                            }
                    }
                        break;
                    case Scope.NearbyGroupMembersExcludingSelf:
                    {
                        if (this is Aisling)
                            foreach (var gc in GetObjects<Aisling>(Map, that =>
                                that.WithinRangeOf(this) && (this as Aisling).GroupParty.Has(that)))
                            {
                                if (!gc.Client.Aisling.CanSeeHidden() && (this as Aisling).Invisible)
                                    if (format is ServerFormat33)
                                        return;

                                if (!gc.Client.Aisling.CanSeeGhosts() && (this as Aisling).Dead)
                                    if (format is ServerFormat33)
                                        return;

                                gc.Client.Send(format);
                            }
                    }
                        break;
                    case Scope.NearbyGroupMembers:
                    {
                        if (this is Aisling)
                            foreach (var gc in GetObjects<Aisling>(Map, that =>
                                that.WithinRangeOf(this) && (this as Aisling).GroupParty.Has(that, true)))
                            {
                                if (!gc.Client.Aisling.CanSeeHidden() && (this as Aisling).Invisible)
                                    if (format is ServerFormat33)
                                        return;

                                if (!gc.Client.Aisling.CanSeeGhosts() && (this as Aisling).Dead)
                                    if (format is ServerFormat33)
                                        return;

                                gc.Client.Send(format);
                            }
                    }
                        break;
                    case Scope.DefinedAislings:
                        if (definer != null)
                            foreach (var gc in definer)
                            {
                                if (this is Aisling)
                                {
                                    if (!gc.Client.Aisling.CanSeeHidden() && (this as Aisling).Invisible)
                                        if (format is ServerFormat33)
                                            return;

                                    if (!gc.Client.Aisling.CanSeeGhosts() && (this as Aisling).Dead)
                                        if (format is ServerFormat33)
                                            return;
                                }

                                (gc as Aisling).Client.Send(format);
                            }

                        break;
                }
            }
            catch (Exception)
            {
<<<<<<< HEAD
                ServerContext.logger.Error("Error in Show<T>");
=======
                ServerContext.SrvLog.Error("Error in Show<T>", err);
>>>>>>> parent of 3e08817... Performance Changes
            }
        }

        public Aisling Aisling(Sprite obj)
        {
            if (obj is Aisling aisling)
                return aisling;

            return null;
        }

        public Monster Monster(Sprite obj)
        {
            if (obj is Monster monster)
                return monster;

            return null;
        }

        private int ComputeDmgFromAc(int dmg, Sprite target)
        {
            var armor = Ac;


            var a = dmg * Math.Abs(armor + 1) / 140;
            var b = (int) (dmg * Math.Abs(armor + 1) * 0.01);


            if (armor < 0)
                dmg += a;
            else
                dmg += b;

            dmg = Math.Abs(dmg);

            return dmg;
        }


        public IEnumerable<Sprite> GetSprites(int x, int y)
        {
            return GetObjects(Map, i => i.XPos == x && i.YPos == y, Get.All);
        }


        public List<Sprite> GetInfront(Sprite sprite, int tileCount = 1)
        {
            return _GetInfront(tileCount).Where(i => i != null && i.Serial != sprite.Serial).ToList();
        }

        public List<Sprite> GetInfront(int tileCount = 1, bool intersect = false)
        {
            return _GetInfront(tileCount).ToList();
        }

        private List<Sprite> _GetInfront(int tileCount = 1)
        {
            var results = new List<Sprite>();

            for (var i = 1; i <= tileCount; i++)
                switch (Direction)
                {
                    case 0:
                        results.AddRange(GetSprites(XPos, YPos - i));
                        break;
                    case 1:
                        results.AddRange(GetSprites(XPos + i, YPos));
                        break;
                    case 2:
                        results.AddRange(GetSprites(XPos, YPos + i));
                        break;
                    case 3:
                        results.AddRange(GetSprites(XPos - i, YPos));
                        break;
                }

            return results;
        }

        public void HideFrom(Aisling nearbyAisling)
        {
            nearbyAisling.Show(Scope.Self, new ServerFormat0E(Serial));
        }

        public void ShowTo(Aisling nearbyAisling)
        {
            if (nearbyAisling != null)
            {
                if (this is Aisling)
                    nearbyAisling.Show(Scope.Self, new ServerFormat33(Client, this as Aisling));
                else
                    nearbyAisling.Show(Scope.Self, new ServerFormat07(new[] {this}));
            }
        }

        public bool WithinRangeOf(Sprite other, bool checkMap = true)
        {
            if (other == null)
                return false;

            return WithinRangeOf(other, ServerContext.Config.WithinRangeProximity, checkMap);
        }

        public bool WithinRangeOf(Sprite other, int distance, bool checkMap = true)
        {
            if (other == null)
                return false;

            if (checkMap)
                if (CurrentMapId != other.CurrentMapId)
                    return false;


            return WithinRangeOf(other.XPos, other.YPos, distance);
        }

        public bool WithinRangeOf(int x, int y, int subjectLength)
        {
            var A = new Point(XPos, YPos);
            var B = new Point(x, y);
            var Dst = Point.Subtract(A, B).Length;

            return (int) Dst <= subjectLength;
        }

        public bool Facing(Sprite other, out int direction)
        {
            return Facing(other.XPos, other.YPos, out direction);
        }

        public bool Facing(int x, int y, out int direction)
        {
            var xDist = (x - XPos).Clamp(-1, +1);
            var yDist = (y - YPos).Clamp(-1, +1);

            direction = DirectionTable[xDist + 1][yDist + 1];
            return Direction == direction;
        }


        public void Remove()
        {
            if (this is Monster)
                Remove<Monster>();

            if (this is Aisling)
                Remove<Aisling>();

            if (this is Money)
                Remove<Money>();

            if (this is Item)
                Remove<Item>();

            if (this is Mundane)
                Remove<Mundane>();
        }

        public Aisling[] AislingsNearby()
        {
            return GetObjects<Aisling>(Map, i => i != null && i.WithinRangeOf(this)).ToArray();
        }

        public Monster[] MonstersNearby()
        {
            return GetObjects<Monster>(Map, i => i != null && i.WithinRangeOf(this)).ToArray();
        }

        public Mundane[] MundanesNearby()
        {
            return GetObjects<Mundane>(Map, i => i != null && i.WithinRangeOf(this)).ToArray();
        }


        /// <summary>
        ///     Use this to Remove Sprites
        ///     It will remove them from ingame to who those effected.
        ///     and invoke the objectmanager.
        /// </summary>
        public void Remove<T>() where T : Sprite, new()
        {
            var nearby = GetObjects<Aisling>(Map, i => i.WithinRangeOf(this));
            var response = new ServerFormat0E(Serial);

            foreach (var o in nearby) o?.Client?.Send(response);

            DeleteObject();
        }

        private void DeleteObject()
        {
            if (this is Monster)
                DelObject(this as Monster);
            if (this is Aisling)
                DelObject(this as Aisling);
            if (this is Money)
                DelObject(this as Money);
            if (this is Item)
                DelObject(this as Item);
            if (this is Mundane)
                DelObject(this as Mundane);
        }

        public void UpdateBuffs(TimeSpan elapsedTime)
        {
            Buff[] buff_Copy;

            lock (Buffs)
            {
                buff_Copy = new List<Buff>(Buffs.Values).ToArray();
            }

            if (buff_Copy.Length == 0)
                return;

            for (var i = 0; i < buff_Copy.Length; i++)
                if (buff_Copy[i] != null)
                    buff_Copy[i].Update(this, elapsedTime);
        }

        public void UpdateDebuffs(TimeSpan elapsedTime)
        {
            Debuff[] debuff_Copy;

            if (Debuffs == null)
                return;

            if (Debuffs.Count == 0)
                return;

            lock (Debuffs)
            {
                debuff_Copy = new List<Debuff>(Debuffs.Values).ToArray();
            }

            if (debuff_Copy.Length == 0)
                return;

            for (var i = 0; i < debuff_Copy.Length; i++)
                if (debuff_Copy[i] != null)
                    debuff_Copy[i].Update(this, elapsedTime);
        }

        /// <summary>
        ///     Show all nearby aislings, this sprite has turned.
        /// </summary>
        public virtual void Turn()
        {
            if (!CanUpdate())
                return;

            if (LastDirection != Direction)
                LastDirection = Direction;

            Show(Scope.NearbyAislings, new ServerFormat11
            {
                Direction = Direction,
                Serial = Serial
            });
        }

        public void WalkTo(int x, int y, bool ignoreWalls = false)
        {
            if (!CanUpdate()) return;

            try
            {
                var buffer = new byte[2];
                var length = float.PositiveInfinity;
                var offset = 0;

                for (byte i = 0; i < 4; i++)
                {
                    var newX = XPos + Directions[i][0];
                    var newY = YPos + Directions[i][1];

                    if (newX == x &&
                        newY == y)
                        continue;

                    if (!ignoreWalls && Map.IsWall(this, newX, newY))
                        continue;

                    var xDist = x - newX;
                    var yDist = y - newY;
                    var tDist = (float) Math.Sqrt(xDist * xDist + yDist * yDist);

                    if (length < tDist)
                        continue;

                    if (length > tDist)
                    {
                        length = tDist;
                        offset = 0;
                    }

                    if (offset < buffer.Length)
                        buffer[offset] = i;

                    offset++;
                }

                if (offset == 0)
                    return;

                lock (rnd)
                {
                    if (offset < buffer.Length)
                        Direction = buffer[rnd.Next(0, offset)];
                }

                if (!Walk())
                    return;
            }
            catch
            {
                // ignored
            }
        }

        public virtual void Wander()
        {
            if (!CanUpdate())
                return;

            var savedDirection = Direction;
            var update = false;

            lock (rnd)
            {
                Direction = (byte) rnd.Next(0, 4);

                if (Direction != savedDirection) update = true;
            }

            if (!Walk() && update)
                Show(Scope.NearbyAislings, new ServerFormat11
                {
                    Direction = Direction,
                    Serial = Serial
                });
        }

        public bool CanUpdate()
        {
            if (IsSleeping || IsFrozen || IsBlind)
                return false;

            if (this is Monster || this is Mundane)
                if (CurrentHp == 0)
                    return false;

            if (!ServerContext.Config.CanMoveDuringReap)
                if (this is Aisling _aisling)
                {
                    if (_aisling.Skulled)
                    {
                        _aisling.Client.SystemMessage(ServerContext.Config.ReapMessageDuringAction);
                        return false;
                    }
                }

            return true;
        }


        public virtual bool Walk()
        {
            var savedX = XPos;
            var savedY = YPos;

            var pendingX = XPos;
            var pendingY = YPos;

            var result = false;


            if (result = TryWalk(pendingX, pendingY, savedX, savedY))
            {
                Map.MapNodes[savedX, savedY].Remove(this);
            }
            else
            {
                Map.MapNodes[savedX, savedY].Add(this);
                Map.MapNodes[pendingX, pendingY].Remove(this);

                if (this is Aisling aisling) aisling.Client.Refresh();
            }


            return result;
        }

        public bool TryWalk(int pendingX, int pendingY, int savedX, int savedY)
        {
            if (!CanUpdate())
            {
                if (this is Aisling aisling) aisling.Client.Refresh();

                return false;
            }

            if (Direction == 0)
            {
                if (this is Aisling
                    ? Map.IsWall(this as Aisling, XPos, YPos - 1)
                    : Map.IsWall(this, XPos, YPos - 1))
                    return false;

                pendingY--;
            }

            if (Direction == 1)
            {
                if (this is Aisling
                    ? Map.IsWall(this as Aisling, XPos + 1, YPos)
                    : Map.IsWall(this, XPos + 1, YPos))
                    return false;

                pendingX++;
            }

            if (Direction == 2)
            {
                if (this is Aisling
                    ? Map.IsWall(this as Aisling, XPos, YPos + 1)
                    : Map.IsWall(this, XPos, YPos + 1))
                    return false;

                pendingY++;
            }

            if (Direction == 3)
            {
                if (this is Aisling
                    ? Map.IsWall(this as Aisling, XPos - 1, YPos)
                    : Map.IsWall(this, XPos - 1, YPos))
                    return false;

                pendingX--;
            }

            pendingX = pendingX.Clamp(pendingX, Map.Cols - 1);
            pendingY = pendingY.Clamp(pendingY, Map.Rows - 1);

            LastPosition = new Position(savedX, savedY);
            {
                Map.MapNodes[pendingX, pendingY].Add(this);

                if (CompleteWalk(pendingX, pendingY, savedX, savedY))
                {
                    var response = new ServerFormat0C
                    {
                        Direction = Direction,
                        Serial = Serial,
                        X = (short) savedX,
                        Y = (short) savedY
                    };

                    Show(Scope.NearbyAislingsExludingSelf, response);
                    return true;
                }

                return false;
            }
        }

        private bool CompleteWalk(int pendingX, int pendingY, int savedX, int savedY)
        {
            if (new Position(savedX, savedY).DistanceFrom(new Position(pendingX, pendingY)) > 1) return false;

            TriggerNearbyTraps();

            if (this is Aisling)
            {
                if (Map.MapNodes[pendingX, pendingY].SpotVacant(this))
                {
                    var result = false;
                    foreach (var spriteObj in Map.MapNodes[pendingX, pendingY].Sprites)
                        if (spriteObj.Serial == Serial)
                        {
                            if (spriteObj.XPos != pendingX || spriteObj.YPos != pendingY)
                            {
                                spriteObj.XPos = pendingX;
                                spriteObj.YPos = pendingY;

                                result = true;
                            }

                            break;
                        }

                    Client.Send(new ServerFormat0B
                    {
                        Direction = Direction,
                        LastX = (ushort) savedX,
                        LastY = (ushort) savedY
                    });


                    return result;
                }
            }
            else
            {
                if (Map.MapNodes[pendingX, pendingY].SpotVacant(this))
                {
                    if (XPos != pendingX)
                        XPos = pendingX;

                    if (YPos != pendingY)
                        YPos = pendingY;
                }

                return true;
            }

            return false;
        }

        public Aisling SendAnimation(ushort Animation, Sprite To, Sprite From, byte speed = 100)
        {
            var format = new ServerFormat29((uint) From.Serial, (uint) To.Serial, Animation, 0, speed);
            {
                Show(Scope.NearbyAislings, format);
            }

            return Aisling(this);
        }

        public void Animate(ushort animation, byte speed = 100)
        {
            Show(Scope.NearbyAislings, new ServerFormat29((uint) Serial, (uint) Serial, animation, animation, speed));
        }

        public void BarMsg(string message, byte type = 0x02)
        {
            var response = new ServerFormat0D
            {
                Serial = Serial,
                Type = type,
                Text = message
            };

            Show(Scope.NearbyAislings, response);
        }

        public void GiveHP(int value)
        {
            _MaximumHp += value;
        }

        public void GiveMP(int value)
        {
            _MaximumMp += value;
        }

        public void Kill()
        {
            CurrentHp = 0;
        }

        public void Update()
        {
            Show(Scope.NearbyAislings, new ServerFormat0E(Serial));
            Show(Scope.NearbyAislings, new ServerFormat07(new[] {this}));
        }

        public void ScrollTo(string destination, short x, short y)
        {
            var map = ServerContext.GlobalMapCache.Where(i =>
                i.Value.Name.Equals(destination, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if (map.Value != null)
                if (this is Aisling)
                {
                    var client = (this as Aisling).Client;

                    client.TransitionToMap(map.Value, new Position(x, y));
                }
        }

        public void SendAnimation(ushort v, Position position)
        {
            Show(Scope.NearbyAislings, new ServerFormat29(v, position.X, position.Y));
        }

        public void ApplyBuff(string buff)
        {
            if (ServerContext.GlobalBuffCache.ContainsKey(buff))
            {
                var Buff = Clone<Buff>(ServerContext.GlobalBuffCache[buff]);

                if (Buff == null || string.IsNullOrEmpty(Buff.Name))
                    return;

                if (!HasBuff(Buff.Name)) Buff.OnApplied(this, Buff);
            }
        }

        public void ApplyDebuff(string debuff)
        {
            if (ServerContext.GlobalDeBuffCache.ContainsKey(debuff))
            {
                var Debuff = Clone<Debuff>(ServerContext.GlobalBuffCache[debuff]);
                if (!HasDebuff(Debuff.Name)) Debuff.OnApplied(this, Debuff);
            }
        }

        public void RefreshStats()
        {
            if (this is Aisling)
                (this as Aisling).Client.SendStats(StatusFlags.All);
        }

        public void WarpTo(Position newLocation)
        {
            Map.Update(X, Y, this, true);
            {
                var location = new Position(newLocation.X, newLocation.Y);

                X = location.X;
                Y = location.Y;
            }

            Map.Update(X, Y, this);
            Update();
        }

        #endregion
    }
}