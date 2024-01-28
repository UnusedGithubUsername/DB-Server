using System; 
using System.Net.Sockets;
using System.Security.Cryptography; 
using System.IO; 

namespace Server {

    public struct CustomizedCharacter {
        public byte[] stats = new byte[4];
        public byte[] statsPerLevel = new byte[4];
        public byte[] skills = new byte[10];
        public byte statpointsFullyAllocated = 0;
        public CustomizedCharacter(byte[] characterData) {
            Buffer.BlockCopy(characterData, 0, stats, 0, stats.Length );
            Buffer.BlockCopy(characterData, 4, statsPerLevel, 0, statsPerLevel.Length );
            Buffer.BlockCopy(characterData, 8, skills, 0, skills.Length );
            statpointsFullyAllocated = characterData[18];
        }

        public CustomizedCharacter(byte[] bStats, byte[] sPLevel, byte[] sklz, byte statpointsFullyAllocatedd) {
            if (bStats.Length != stats.Length || sPLevel.Length != statsPerLevel.Length || skills.Length != sklz.Length) {
                throw new IndexOutOfRangeException("Customized character expects different values");
            }
            Buffer.BlockCopy(bStats,    0, stats,       0, stats.Length );
            Buffer.BlockCopy(sPLevel,   0, statsPerLevel,   0, statsPerLevel.Length );
            Buffer.BlockCopy(sklz,      0, skills,          0, skills.Length );
            statpointsFullyAllocated = statpointsFullyAllocatedd;
        }

        public byte[] ToByte() {
            byte[] dataAsByte = new byte[19];

            Buffer.BlockCopy(stats,     0,      dataAsByte, 0, stats.Length);
            Buffer.BlockCopy(statsPerLevel, 0,      dataAsByte, stats.Length, statsPerLevel.Length);
            Buffer.BlockCopy(skills,        0,      dataAsByte, stats.Length + statsPerLevel.Length, skills.Length);
            dataAsByte[18] = statpointsFullyAllocated;
            return dataAsByte; 
        }
    }

    public enum Skill {
        MassSpells = 0,  // Skill that enables AoE on Spells
        ArcaneKnowledge = 1, //learn level 3 spells
        PrimalAttunement = 2, //Choose a second attuned magic school, your first attuned school decreases Spellslotcost by 2;
        NeuralFastpass = 3, //Consecutive uses of Skills and Spells reduces the casting cost for further uses by 20%, One Skill and one spell can have up to 3 Stacks at the same time

    }

    public enum PacketTypeClient {
         
        Login, 
        requestWithToken 
    }

    public enum PacketTypeServer {

        publicKeyPackage,
        LoginSuccessfull,
        loginFailed, 
        CharacterData,
        levelupSuccessfull
    }

    public struct StreamResult {
        public byte[] data;
        public int dataIndex = 0;

        public StreamResult(ref Socket connection, ref RSACryptoServiceProvider rsa) {
            //in order to have secure communication for passwords etc, we need encryption. But its to cumbersome to encrypt everything, so only 
            //the important stuff is encrypted. encryption expands the size to 128 bytes even though the source data is only 52 bytes in the case of login
            //thats why several blockcopies are needed

            data = new byte[connection.Available];//create new array
            connection.Receive(data); //Read all data. the first 128 bytes are encrypted. the decrypted 128bytes are usually far fewer bytes
             
            //decrypt the first 128 bytes and write them to data[]
            byte[] dataToDecrypt = new byte[128];
            Buffer.BlockCopy(data, 4, dataToDecrypt, 0, 128); 
            dataToDecrypt = rsa.Decrypt(dataToDecrypt, false);
             
            byte[] decryptedFullData = new byte[dataToDecrypt.Length + data.Length - 128];
            Buffer.BlockCopy(dataToDecrypt, 0, decryptedFullData, 4, dataToDecrypt.Length);
            Buffer.BlockCopy(data, 0, decryptedFullData, 0, 4);
            Buffer.BlockCopy(data, 132, decryptedFullData, dataToDecrypt.Length + 4, data.Length - 132);
            data = decryptedFullData;

        }

        public string ReadString() {
            int stringLength = this.ReadInt();
            string result = System.Text.Encoding.UTF8.GetString(data, dataIndex, stringLength);
            dataIndex += stringLength;

            return result;
        }
          
        public byte[] ReadBytes() {
            int stringLength = this.ReadInt();
            byte[] stringArray = new byte[stringLength];//create new array, 1 byte per char  
            Buffer.BlockCopy(data, dataIndex, stringArray, 0, stringLength);
            dataIndex += stringLength;

            return stringArray;
        }

        public int ReadInt() {
            int intRead = BitConverter.ToInt32(data, dataIndex);
            dataIndex += 4;
            return intRead;
        }
          
        public int BytesLeft() {
            return data.Length - dataIndex;
        }
    }

    public static class ServerHelper {
        public const string BasePath = "C:\\Users\\Klauke\\Documents\\My Games\\Corivi\\LauncherServer\\";

        public static void EnsureFolderExists(string folderName) {
            if (!Directory.Exists(folderName)) {
                Directory.CreateDirectory(folderName);
            }
        }

        public static void Send(ref Socket s, PacketTypeServer type, byte[] data) //Send the data and append the data length and the packet type
        {
            byte[] packetLength = BitConverter.GetBytes(data.Length + 4);
            byte[] packetType = BitConverter.GetBytes((int)type);
            byte[] fullData = CombineBytes(packetLength, packetType, data);

            s.Send(fullData, 0, fullData.Length, SocketFlags.None); 
        }
         
        public static byte[] CombineBytes(byte[] first, byte[] second) {
            byte[] bytes = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, bytes, 0, first.Length);
            Buffer.BlockCopy(second, 0, bytes, first.Length, second.Length);
            return bytes;
        }

        public static byte[] CombineBytes(byte[] first, byte[] second, byte[] third) {
            byte[] bytes = new byte[first.Length + second.Length + third.Length];
            Buffer.BlockCopy(first, 0, bytes, 0, first.Length);
            Buffer.BlockCopy(second, 0, bytes, first.Length, second.Length);
            Buffer.BlockCopy(third, 0, bytes, first.Length + second.Length, third.Length);
            return bytes;
        } 
    }
}
