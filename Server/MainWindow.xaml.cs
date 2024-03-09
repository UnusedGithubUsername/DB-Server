using Npgsql;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace Server {

    public partial class MainWindow : Window {

        public const int MAX_FILE_BYTES = 65524;
        private const int ALLOWED_CONSOLE_LENGTH = 20;
        private readonly List<String> Chatoutput = new();

        private const int port = 16501;
        private readonly TcpListener listener;
        private readonly List<Socket> connections;

        private RSACryptoServiceProvider rsa;
        private readonly string publicKey;
        private readonly string privateKey;
        private readonly Dictionary<int, int> loginSessions = new();//lookup if a client is logged in, so he does not

        Socket gameServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        DateTime lastGameServerMessage = DateTime.Now;
        bool gameServerConnected = false;

        private NpgsqlConnection database;

        //check login credentials
        private NpgsqlCommand Cmd_Logincheck;
        private NpgsqlParameter CmdP_Logincheck_email;
        private NpgsqlParameter CmdP_Logincheck_password; //hashed salted and encrypted pw, dont forget to decrypt before asking db

        //retrieve inventory that we can send to a client
        private NpgsqlCommand Cmd_getItems;
        private NpgsqlParameter CmdP_getItems_ItemID;

        //get the type of a character from a player to validate a clients request
        private NpgsqlCommand Cmd_getItemType;
        private NpgsqlParameter CmdP_getItemType_PlayerGUID;
        private NpgsqlParameter CmdP_getItemType_ItemIndex;

        //get the type of a character from a player to validate a clients request
        private NpgsqlCommand Cmd_getItemXp;
        private NpgsqlParameter CmdP_getItemXp_PlayerGUID;
        private NpgsqlParameter CmdP_getItemXp_ItemIndex;

        //get the type of a character from a player to validate a clients request
        private NpgsqlCommand Cmd_getUserXp;
        private NpgsqlParameter CmdP_getUserXp_PlayerGUID;

        //levelup a character by one level
        private NpgsqlCommand Cmd_levelup;
        private NpgsqlParameter CmdP_levelup_XP;
        private NpgsqlParameter CmdP_levelup_GUID;
        private NpgsqlParameter CmdP_levelup_ItemIndex; 

        //have to send over username and password every time. Guid is the key, the session is the value
        public MainWindow() {
            InitializeComponent();
            ConnestToDB();

            rsa = new RSACryptoServiceProvider();
            publicKey = rsa.ToXmlString(false);
            privateKey = rsa.ToXmlString(true);
            rsa.FromXmlString(privateKey);

            connections = new List<Socket>();
            IPAddress ip = IPAddress.Parse("84.186.12.173");

            listener = new TcpListener(IPAddress.Any, port);//loopback means localhost
            listener.Start(10);

            Timer updateLoop = new(100);//create an update loop that runs 10 times per second to scan for connections and data
            updateLoop.Elapsed += Update;
            updateLoop.Enabled = true;
            updateLoop.AutoReset = true;
            updateLoop.Start();

            ReachGameServer(); 
        }


        private async void ReachGameServer() { 

            IPAddress ip = IPAddress.Parse("84.186.12.173");
            while (true) { //continously try to connect to Game. And check if connection is still active
                await Task.Delay(500);
                if (!gameServerConnected) {
                    try {
                        await gameServer.ConnectAsync(ip, 16515); 
                        SendPKeyPackage(gameServer);

                        lastGameServerMessage = DateTime.Now;
                        gameServerConnected = true;
                    }
                    catch (Exception e) {
                        Chatoutput.Add("Server not Reachable \n"); 
                        UpdateConsole(true, Chatoutput.Count);
                    }  
                }
                else { 
                    if ((DateTime.Now - lastGameServerMessage).TotalSeconds > 2.0f) {

                        gameServerConnected = false; 
                        gameServer.Close();
                        gameServer.Dispose();
                        gameServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); 
                    }
                }
            }
        }

        private async void ConnestToDB() {
            await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create("Host=localhost;Port=5432;Username=postgres;Password=qwert;Database=qqq_game");
            database = dataSource.OpenConnection();

            string preparedStatement = "SELECT GUId, username FROM users where email = (@email);";
            Cmd_Logincheck = new NpgsqlCommand(preparedStatement, database);
            CmdP_Logincheck_email = Cmd_Logincheck.Parameters.Add(new NpgsqlParameter { ParameterName = "email", NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Varchar });
            Cmd_Logincheck.Prepare();

            string preparedStatementItems = "SELECT item_guid, user_guid, item_id, xp, level FROM items where user_guid = (@user_guid);";
            Cmd_getItems = new NpgsqlCommand(preparedStatementItems, database);
            CmdP_getItems_ItemID = Cmd_getItems.Parameters.Add(new NpgsqlParameter { ParameterName = "user_guid", NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
            Cmd_getItems.Prepare();

            string preparedStatementCharacterType = "SELECT item_id FROM items WHERE user_guid = (@user_guid) AND item_guid = (@item_guid);";
            Cmd_getItemType = new NpgsqlCommand(preparedStatementCharacterType, database);
            CmdP_getItemType_PlayerGUID = Cmd_getItemType.Parameters.Add(new NpgsqlParameter { ParameterName = "user_guid", NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
            CmdP_getItemType_ItemIndex = Cmd_getItemType.Parameters.Add(new NpgsqlParameter { ParameterName = "item_guid", NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
            Cmd_getItemType.Prepare();

            string preparedStatementCharacterXp = "SELECT xp FROM items WHERE user_guid = (@user_guid) AND item_guid = (@item_guid);";
            Cmd_getItemXp = new NpgsqlCommand(preparedStatementCharacterXp, database);
            CmdP_getItemXp_PlayerGUID = Cmd_getItemXp.Parameters.Add(new NpgsqlParameter { ParameterName = "user_guid", NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
            CmdP_getItemXp_ItemIndex = Cmd_getItemXp.Parameters.Add(new NpgsqlParameter { ParameterName = "item_guid", NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
            Cmd_getItemXp.Prepare();

            string preparedStatementUserXp = "SELECT xp FROM items WHERE user_guid = (@user_guid) AND item_id = 1;";
            Cmd_getUserXp = new NpgsqlCommand(preparedStatementUserXp, database);
            CmdP_getUserXp_PlayerGUID = Cmd_getUserXp.Parameters.Add(new NpgsqlParameter { ParameterName = "user_guid", NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
            Cmd_getUserXp.Prepare();

            string preparedStatementLevelup =
                "UPDATE items SET xp = xp + (@xp_value) WHERE user_guid = (@user_guid) AND item_guid = (@item_guid);" +
                "UPDATE items SET xp = xp - (@xp_value) WHERE user_guid = (@user_guid) AND item_id = 1;";
            Cmd_levelup = new NpgsqlCommand(preparedStatementLevelup, database);
            CmdP_levelup_XP = Cmd_levelup.Parameters.Add(new NpgsqlParameter { ParameterName = "xp_value", NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
            CmdP_levelup_GUID = Cmd_levelup.Parameters.Add(new NpgsqlParameter { ParameterName = "user_guid", NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
            CmdP_levelup_ItemIndex = Cmd_levelup.Parameters.Add(new NpgsqlParameter { ParameterName = "item_guid", NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
            Cmd_levelup.Prepare();

        }

        private void Update(object sender, ElapsedEventArgs e) {

            while (listener.Pending()) {
                Socket connection = listener.AcceptSocket();//accept new connections ...
                connections.Add(connection);

                //... and respond with a public key, so that the clients can send their credentials encrypted 
                SendPKeyPackage(connection);
                Chatoutput.Add("Accepted a connection and SEND A PKEY" + "\n");
                UpdateConsole(true, Chatoutput.Count);
            }

            //all the communication happens here
            for (int i = 0; i < connections.Count; i++) {
                if (connections[i].Available > 0) {
                    HandleConnectionRequest(connections[i]);
                }
            }

            if(gameServerConnected)
                if(gameServer.Available > 0)
                    HandleConnectionRequest(gameServer);

            //handle disconnect
            for (int i = 0; i < connections.Count; i++) {
                if (connections[i].Connected == false) {
                    connections.RemoveAt(i);
                    i--;
                }
            }
        }

        private Socket SendPKeyPackage(Socket connection) {
            int byteLengthOfPublicKey = publicKey.Length;
            byte[] pKeyLen = BitConverter.GetBytes(byteLengthOfPublicKey);
            byte[] pKeyBytes = Encoding.UTF8.GetBytes(publicKey);
            byte[] intBytes = ServerHelper.CombineBytes(pKeyLen, pKeyBytes);

            //connection.Send(intBytes, 0, intBytes.Length, SocketFlags.None);
            ServerHelper.Send(ref connection, PacketTypeServer.publicKeyPackage, intBytes);
            return connection;
        }

        private async void HandleConnectionRequest(Socket connection) {

            StreamResult result = new(ref connection, ref rsa);
            PacketTypeClient type = (PacketTypeClient)result.ReadInt();
            int clientToken = result.ReadInt();

            switch (type) {
                case PacketTypeClient.ForewardLoginPacket:
                    Chatoutput.Add("recieved forewarded Login");
                    byte[] fwdLoginPacket = result.ReadBytes();
                    int actualNetID = result.ReadInt(); 
                    Chatoutput.Add(fwdLoginPacket.Length.ToString()+" bytes forewarded Login; netID="+actualNetID.ToString()+" \n");
                    result.ConvertForewardedLoginPackage(ref fwdLoginPacket, ref rsa); 
                    string name = result.ReadString();
                    string encPW = result.ReadString();
                    int netID2 = result.ReadInt();
                    Chatoutput.Add(netID2.ToString()+" name= " + name + " pw=" +encPW +"\n");
                    Login(name, encPW, connection, clientToken, actualNetID);
                    break;

                case PacketTypeClient.Login:
                    string email = result.ReadString();
                    string encryptedPassword = result.ReadString();
                    int netID = result.ReadInt();
                    Chatoutput.Add("A Client is trying to log in\n");
                    Login(email, encryptedPassword, connection, clientToken, netID);
                    break;

                case PacketTypeClient.requestWithToken:
                    int userGUID = result.ReadInt();
                    int requestType = result.ReadInt();
                    int characterGuid = result.ReadInt();

                    bool isValidLogin = (loginSessions[userGUID] == clientToken);//if the client has a verified active session
                    if (!isValidLogin)
                        return;

                    int characterType = await GetItemType(userGUID, characterGuid);
                    bool isValidCharacter = characterType > 1000;
                    if (!isValidCharacter)
                        break;

                    if (requestType == 0) { //save data
                        byte[] characterData = result.ReadBytes();
                        byte[] newCharacterData = await CharacterDB.SaveCharacterData(userGUID, characterGuid, characterData, this);
                        ServerHelper.Send(ref connection, PacketTypeServer.CharacterData, ServerHelper.CombineBytes(BitConverter.GetBytes(characterGuid), BitConverter.GetBytes(newCharacterData.Length), newCharacterData));

                    }
                    else if (requestType == 1) {//get a certain characters data  
                        byte[] characterData = CharacterDB.getCharacterData(userGUID, characterGuid, characterType).ToByte();
                        ServerHelper.Send(ref connection, PacketTypeServer.CharacterData, ServerHelper.CombineBytes(BitConverter.GetBytes(characterGuid), BitConverter.GetBytes(characterData.Length), characterData));
                    }
                    else if (requestType == 2) { //Levelup
                        int requestedLevel = result.ReadInt();
                        if (requestedLevel <= 10 && requestedLevel > 0) {
                            int successfullLevelup = await LevelupCharacter(requestedLevel, characterGuid, userGUID);
                            byte[] levelUpResult = ServerHelper.CombineBytes(BitConverter.GetBytes(successfullLevelup), BitConverter.GetBytes(characterGuid), BitConverter.GetBytes(requestedLevel));
                            ServerHelper.Send(ref connection, PacketTypeServer.levelupSuccessfull, levelUpResult);
                        }
                    }
                    else if (requestType == 3) {
                        byte[] characterData = CharacterDB.ResetStats(userGUID, characterGuid, characterType);
                        ServerHelper.Send(ref connection, PacketTypeServer.CharacterData, ServerHelper.CombineBytes(BitConverter.GetBytes(characterGuid), BitConverter.GetBytes(characterData.Length), characterData));

                    }

                    break;
                case (PacketTypeClient.KeepAlive):
                    lastGameServerMessage = DateTime.Now;
                    break;
                     
                default:
                    break;
            }
        }

        private async Task<int> LevelupCharacter(int requestedLevel, int itemIndex, int userGuid) {
            Chatoutput.Add("Character wants to level up to level " + requestedLevel.ToString() + "\n");
            UpdateConsole(true, Chatoutput.Count);

            int characterXP = await GetCharacterXP(itemIndex, userGuid);
            int userXP = await GetUserXP(userGuid);
            int actualLevel = (int)Math.Sqrt((double)characterXP / 10); // Sum (n+n-1) starting at n= 1 =  n^2 , so converting back is just a sqare root
            int nextLevel = actualLevel + 1;
            int requiredXP = (nextLevel * 2 - 1) * 10; //  (n+n-1) is equal to 2n-1

            if (requestedLevel - 1 != actualLevel) {
                Chatoutput.Add("ERROR, client had different level data, maybe clicked too fast \n");
                return -1;
            }
            if (userXP < requiredXP)
                return -1;

            SetLevelup(itemIndex, userGuid, requiredXP);
            int xp_remaining = userXP - requiredXP;
            return xp_remaining;
        }

        public void SetLevelup(int itemIndex, int userGuid, int xp_value) { 
            CmdP_levelup_ItemIndex.Value = itemIndex;
            CmdP_levelup_GUID.Value = userGuid;
            CmdP_levelup_XP.Value = xp_value;
            Cmd_levelup.ExecuteNonQuery();
        }

        public async Task<int> GetCharacterXP(int itemIndex, int userGuid) { 
            CmdP_getItemXp_ItemIndex.Value = itemIndex;
            CmdP_getItemXp_PlayerGUID.Value = userGuid; 
            return await GetSingleValueFromQuery(Cmd_getItemXp);
        }

        public async Task<int> GetUserXP(int userGuid) { 
            CmdP_getUserXp_PlayerGUID.Value = userGuid; 
            return await GetSingleValueFromQuery(Cmd_getUserXp); 
        }

        public async Task<int> GetItemType(int userGuid, int item_guid) {
            CmdP_getItemType_PlayerGUID.Value = userGuid;
            CmdP_getItemType_ItemIndex.Value = item_guid; 
            return await GetSingleValueFromQuery(Cmd_getItemType);
        }

        public async Task<int> GetSingleValueFromQuery(NpgsqlCommand cmd) {
            await using NpgsqlDataReader reader = cmd.ExecuteReader();
            int value = reader.Read() ? reader.GetInt32(0) : -1;//if data is available to be read, that means we got itemID back
            reader.Close();
            return value;
        }

        private async void Login(string name, string encryptedPassword, Socket connection, int clientToken, int netID) {// netID for game doesnt log in, just checks credentials

            //Step1: Retrieve the Guid of the Account. If we cant, the login failed
            CmdP_Logincheck_email.Value = name;
            await using NpgsqlDataReader reader = Cmd_Logincheck.ExecuteReader();
            if (!reader.Read())//if no matching account is found, the login failed
            {
                Chatoutput.Add("A Client failed to log in, name = " + name + "\n");
                ServerHelper.Send(ref connection, PacketTypeServer.loginFailed, Array.Empty<byte>());
                reader.Close();
                return;
            }
            int Guid = reader.GetInt32(0); //get the first value in the array (the array is 1 long, and the first and only value is the guid
            string username = reader.GetString(1);
            reader.Close();

            Chatoutput.Add("A Client logged int: GUId = " + Guid.ToString() + " , name = " + username + " , email = " + name + "\n");

            //this is what actually logs in the client. but the game server will also check logins, 
            //the game server will send a netID so that it can reidentify the gameClient who logs in
            //we use the existence of the netID as an indicator that we dont actually wanne log in
            if(netID == 0)
                loginSessions[Guid] = clientToken; //add or update dictionary, but only if the netID is 0, because only gameServer sends netIDs to reidentify the player

            //now retrieve the clients items
            CmdP_getItems_ItemID.Value = Guid;
            await using NpgsqlDataReader itemReader = Cmd_getItems.ExecuteReader();
            byte[] items = new byte[5000];
            int itemsOffset = 0;
            while (itemReader.Read()) {
                // read result and copy all the items 5 data values (guid, userID, itemID,xp,level) into an array
                for (int i = 0; i < 5; i++) {
                    int value = reader.GetInt32(i); // Assuming the first column is an integer
                    byte[] data = BitConverter.GetBytes(value);
                    Buffer.BlockCopy(data, 0, items, itemsOffset, data.Length);
                    itemsOffset += 4;
                }
            }
            itemReader.Close();

            byte[] itemData = new byte[itemsOffset];//now make an array of the correct size instead of 5000 large
            Buffer.BlockCopy(items, 0, itemData, 0, itemsOffset);
            byte[] combinedData = ServerHelper.CombineBytes(BitConverter.GetBytes(Guid), BitConverter.GetBytes(itemsOffset), itemData);
            byte[] nameLen = BitConverter.GetBytes(username.Length);
            byte[] namebytes = Encoding.UTF8.GetBytes(username);
            combinedData = ServerHelper.CombineBytes(combinedData, BitConverter.GetBytes(netID));
            combinedData = ServerHelper.CombineBytes(combinedData, nameLen, namebytes);
            //ALSO SEND EMAIL; NAME AND FRIENDSLIST!!!
            //byte[] name = 
            ServerHelper.Send(ref connection, PacketTypeServer.LoginSuccessfull, combinedData);

            UpdateConsole(true, Chatoutput.Count);
        }

        private void UpdateConsole(bool snapConsoleToBottom, int indexOfFirstConsoleline) {
            this.Dispatcher.Invoke(() =>
            {
                TextfieldConCount.Content = connections.Count.ToString();

                ScrollBar.ViewportSize = Chatoutput.Count < (ALLOWED_CONSOLE_LENGTH) + 1 ? (float)100 : (float)100 / ((float)Chatoutput.Count * 5f);
                if (snapConsoleToBottom) {
                    ScrollBar.Value = 1;
                }
                int lastEntry = Math.Min(Chatoutput.Count, indexOfFirstConsoleline + ALLOWED_CONSOLE_LENGTH);
                int startIndex = Math.Max(lastEntry - ALLOWED_CONSOLE_LENGTH, 0);

                string output = "";
                for (int i = startIndex; i < lastEntry; i++) {
                    output += Chatoutput[i];
                }
                Console.Text = output;

            });
        }

        private void ScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            Console.Text += e.NewValue.ToString() + "\n";

            float percentageOfScrollbar = Math.Max(0, (float)e.NewValue - 0.5f / Math.Max(1, (Chatoutput.Count - 10)));

            //e.NewValue is a double between 0 and 1 
            float index = percentageOfScrollbar * Chatoutput.Count;
            UpdateConsole(false, (int)index);
        }

        private void ButtonTerminate_Click(object sender, RoutedEventArgs e) {
            for (int i = 0; i < connections.Count; i++) {
                connections[i].Close();
            }
            connections.Clear();
        }

        private void ButtonBuildCharacterFiles_Click(object sender, RoutedEventArgs e) {
            StaticData.BuildBaseValues();
        }

        private async void ButtonResetInventories_Click(object sender, RoutedEventArgs e) {

            int[,] itemsToAdd = {
                {1001, 0},
                {1002, 0},
                {1003, 0},
                {1, 2000}
            };
            //1) TRUNCATE TABLE items
            using NpgsqlCommand cmd0 = new NpgsqlCommand("TRUNCATE TABLE items", database);
            cmd0.ExecuteNonQuery();


            //2) get all guids from table. 
            List<int> playerGuids = new List<int>();

            using NpgsqlCommand cmd = new NpgsqlCommand("SELECT guid FROM users", database);
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (reader.Read()) {
                playerGuids.Add(reader.GetInt32(0));
            }
            reader.Close();

            for (int i = 0; i < playerGuids.Count; i++) {
                for (int j = 0; j < itemsToAdd.GetLength(0); j++) {
                    string addItem = "INSERT INTO items (user_guid, item_id, xp, level ) VALUES (" + playerGuids[i].ToString() + ", " + itemsToAdd[j, 0].ToString() + ", " + itemsToAdd[j, 1].ToString() + ", 1)";
                    using NpgsqlCommand cmd2 = new NpgsqlCommand(addItem, database);
                    cmd2.ExecuteNonQuery();
                }
            }
        }
    }
}
