﻿using System.Linq;
using Darkages.Network.ServerFormats;
using Darkages.Network.ClientFormats;
using Darkages.Scripting;
using Darkages.Templates;
using Darkages.Common;
using Darkages.Compression;
using Darkages.IO;
using Darkages.Types;
using System.Collections.Concurrent;
using System.Collections;
using Darkages;
using Darkages.Storage.locales.Buffs;
using Darkages.Storage.locales.debuffs;
using System.Collections.Generic;
using System;
using Darkages.Network.Game;


namespace Darkages.Storage.locales.Scripts.Areas
{
    [Script("Mileth Altar", "Pill", "Area Script to handle an altar event.")]
    public class MilethAltar : AreaScript
    {
        public MilethAltar(Area area) : base(area)
        {

        }

        public override void Update(TimeSpan elapsedTime)
        {

        }

        public override void OnMapEnter(GameClient client)
        {

        }

        public override void OnMapExit(GameClient client)
        {

        }

        public override void OnPlayerWalk(GameClient client, Position oldLocation, Position newLocation)
        {

        }

        public override void OnItemDropped(GameClient client, Item itemDropped, Position locationDropped)
        {
            //TODO add logic here for when an item is dropped.
            
            //this will remove it from the world. on dropped.
            itemDropped.Remove();
        }
    }
}
