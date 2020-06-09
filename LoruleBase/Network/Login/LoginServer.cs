﻿//Project Lorule: A Dark Ages Client (http://darkages.creatorlink.net/index/)
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

// ReSharper disable RedundantAssignment
// ReSharper disable IdentifierTypo

using System.Linq;
using Newtonsoft.Json;

namespace Darkages.Network.Login
{
    using ClientFormats;
    using ServerFormats;
    using Storage;
    using Types;
    using System;
    using System.Net;
    using System.Text;

    public class LoginServer : NetworkServer<LoginClient>
    {
        public LoginServer(int capacity)
            : base(capacity)
        {
            MServerTable = MServerTable.FromFile("MServerTable.xml");
            Notification = Notification.FromFile("notification.txt");
        }

        public static MServerTable MServerTable { get; set; }
        public static Notification Notification { get; set; }


        /// <summary>
        ///     Send Encryption Parameters.
        /// </summary>
        protected virtual void Format00Handler(LoginClient client, ClientFormat00 format)
        {
            if (ServerContextBase.GlobalConfig.UseLobby)
            {
                if (format.Version == ServerContextBase.GlobalConfig.ClientVersion)
                    client.Send(new ServerFormat00
                    {
                        Type = 0x00,
                        Hash = MServerTable.Hash,
                        Parameters = client.Encryption.Parameters
                    });
            }

            if (ServerContextBase.GlobalConfig.DevMode)
            {
                var aisling = StorageManager.AislingBucket.Load(ServerContextBase.GlobalConfig.GameMaster);

                if (aisling != null)
                    LoginAsAisling(client, aisling);
            }
        }

        /// <summary>
        ///     Login Client - Create New Aisling, Choose Username/password.
        /// </summary>
        protected override void Format02Handler(LoginClient client, ClientFormat02 format)
        {
            //save information to memory.
            client.CreateInfo = format;

            var aisling = StorageManager.AislingBucket.Load(format.AislingUsername);

            if (aisling == null)
            {
                client.SendMessageBox(0x00, "\0");
            }
            else
            {
                client.SendMessageBox(0x03, "Character Already Exists.\0");
                client.CreateInfo = null;
            }
        }

        /// <summary>
        ///     Login Client - Save Character Template.
        /// </summary>
        protected override void Format04Handler(LoginClient client, ClientFormat04 format)
        {
            //make sure the first step was done first.
            if (client.CreateInfo == null)
            {
                ClientDisconnected(client);
                return;
            }

            //create aisling from default template.
            var template = Aisling.Create();
            template.Display = (BodySprite) (format.Gender * 16);
            template.Username = client.CreateInfo.AislingUsername;
            template.Password = client.CreateInfo.AislingPassword;
            template.Gender = (Gender) format.Gender;
            template.HairColor = format.HairColor;
            template.HairStyle = format.HairStyle;

            StorageManager.AislingBucket.Save(template);
            client.SendMessageBox(0x00, "\0");
        }

        /// <summary>
        ///     Login - Check username/password. Proceed to Game Client.
        /// </summary>
        protected override void Format03Handler(LoginClient client, ClientFormat03 format)
        {
            Aisling aisling = null;

            try
            {
                aisling = StorageManager.AislingBucket.Load(format.Username);

                if (aisling != null)
                {
                    if (aisling.Password != format.Password)
                    {
                        client.SendMessageBox(0x02, "Sorry, Incorrect Password.");
                        return;
                    }
                }
                else
                {
                    client.SendMessageBox(0x02,
                        $"{format.Username} does not exist in this world. You can make this hero by clicking on 'Create'.");
                    return;
                }
            }
            catch (Exception e)
            {
                client.SendMessageBox(0x02, $"{format.Username} is not supported by the new server. Please remake your character. This will not happen when the server goes to beta.");

                return;
            }

            if (!ServerContextBase.GlobalConfig.MultiUserLogin)
            {
                var aislings = ServerContextBase.Game.Clients.Where(i => i?.Aisling != null && i.Aisling.LoggedIn && i.Aisling.Username.ToLower() == format.Username.ToLower());

                foreach (var obj in aislings)
                    obj.Server.ClientDisconnected(obj);

            }

            LoginAsAisling(client, aisling);
        }

        public void LoginAsAisling(LoginClient client, Aisling aisling)
        {
            if (aisling != null)
            {
                var map = ServerContext.GlobalMapCache.FirstOrDefault().Value;

                var redirect = new Redirect
                {
                    Serial = Convert.ToString(client.Serial),
                    Salt   = Encoding.UTF8.GetString(client.Encryption.Parameters.Salt),
                    Seed   = Convert.ToString(client.Encryption.Parameters.Seed),
                    Name   = JsonConvert.SerializeObject(new { player = aisling.Username, map } ),
                };

                if (aisling.Username.Equals(ServerContextBase.GlobalConfig.GameMaster,
                    StringComparison.OrdinalIgnoreCase)) aisling.GameMaster = true;

                aisling.Redirect = redirect;

                StorageManager.AislingBucket.Save(aisling);

                client.SendMessageBox(0x00, "\0");
                client.Send(new ServerFormat03
                {
                    EndPoint = new IPEndPoint(Address, ServerContextBase.DefaultPort),
                    Redirect = redirect
                });
            }
        }

        /// <summary>
        ///     Client Closed Connection (Closed Darkages.exe), Remove them.
        /// </summary>
        protected override void Format0BHandler(LoginClient client, ClientFormat0B format)
        {
            RemoveClient(client);
        }

        /// <summary>
        ///     Redirect Client from Lobby Client to Login Client Automatically.
        /// </summary>
        protected override void Format10Handler(LoginClient client, ClientFormat10 format)
        {
            client.Encryption.Parameters = format.Parameters;
            client.Send(new ServerFormat60
            {
                Type = 0x00,
                Hash = Notification.Hash
            });
        }

        /// <summary>
        ///     Login Client - Update Password.
        /// </summary>
        protected override void Format26Handler(LoginClient client, ClientFormat26 format)
        {
            var aisling = StorageManager.AislingBucket.Load(format.Username);

            if (aisling == null)
            {
                client.SendMessageBox(0x02, "Incorrect Information provided.");
                return;
            }

            if (aisling.Password != format.Password)
            {
                client.SendMessageBox(0x02, "Incorrect Information provided.");
                return;
            }

            if (string.IsNullOrEmpty(format.NewPassword) || format.NewPassword.Length < 3)
            {
                client.SendMessageBox(0x02, "new password not accepted.");
                return;
            }

            //Update new password.
            aisling.Password = format.NewPassword;
            //Update and Store Information.
            StorageManager.AislingBucket.Save(aisling);

            client.SendMessageBox(0x00, "\0");
        }

        protected override void Format4BHandler(LoginClient client, ClientFormat4B format)
        {
            client.Send(new ServerFormat60
            {
                Type = 0x01,
                Size = Notification.Size,
                Data = Notification.Data
            });
        }

        protected override void Format57Handler(LoginClient client, ClientFormat57 format)
        {
            if (format.Type == 0x00)
            {
                var redirect = new Redirect
                {
                    Serial = Convert.ToString(client.Serial),
                    Salt = Encoding.UTF8.GetString(client.Encryption.Parameters.Salt),
                    Seed = Convert.ToString(client.Encryption.Parameters.Seed),
                    Name = "socket[" + client.Serial + "]"
                };

                client.Send(new ServerFormat03
                {
                    EndPoint = new IPEndPoint(MServerTable.Servers[0].Address, MServerTable.Servers[0].Port),
                    Redirect = redirect
                });
            }
            else
            {
                client.Send(new ServerFormat56
                {
                    Size = MServerTable.Size,
                    Data = MServerTable.Data
                });
            }
        }

        protected override void Format68Handler(LoginClient client, ClientFormat68 format)
        {
            client.Send(new ServerFormat66());
        }

        protected override void Format7BHandler(LoginClient client, ClientFormat7B format)
        {
            if (format.Type == 0x00)
            {
                Console.WriteLine("Client Requested Metafile: {0}", format.Name);

                client.Send(new ServerFormat6F
                {
                    Type = 0x00,
                    Name = format.Name
                });
            }

            if (format.Type == 0x01)
                client.Send(new ServerFormat6F
                {
                    Type = 0x01
                });
        }

        public override void ClientConnected(LoginClient client)
        {
            client.Send(new ServerFormat7E());
        }
    }
}