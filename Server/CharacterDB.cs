using System; 
using System.Threading.Tasks;
using System.IO;

namespace Server {
    internal class CharacterDB {  
        public static async Task<byte[]> SaveCharacterData(int Guid, int characterGuid, byte[] data, MainWindow main) {

            CustomizedCharacter ClientRequestedCharSave = new(data);
            ClientRequestedCharSave.statpointsFullyAllocated = CalcAllocatedPoints(ClientRequestedCharSave.stats);

            //get the current serverside characterdata, either from saveFile or from basevalues 
            int characterType = await main.GetItemType(Guid, characterGuid);
            CustomizedCharacter serversideCharacter = getCharacterData(Guid, characterGuid, characterType);
             
            //check if the character has sufficient xp i.e. levels to allocate all the stats he requests
            int characterXP = await main.GetCharacterXP(characterGuid, Guid);
            int characterLevel = (int)Math.Sqrt(characterXP / 10); 
            int statsLeftToAllocate = (characterLevel*2) - serversideCharacter.statpointsFullyAllocated;
            if (ClientRequestedCharSave.statpointsFullyAllocated > statsLeftToAllocate)  
                return serversideCharacter.ToByte();//if not, send back the last valid character
            
            //if changes are valid, add requested delta to the current stats, save that and send it back 
            ClientRequestedCharSave.stats = AddStats(ClientRequestedCharSave.stats, serversideCharacter.stats);
            ClientRequestedCharSave.statpointsFullyAllocated += serversideCharacter.statpointsFullyAllocated;
            byte[] dataToSave = ClientRequestedCharSave.ToByte();
            File.WriteAllBytes(GetPath(Guid, characterGuid), dataToSave);
            return dataToSave;
        }

        public static byte[] ResetStats(int Guid, int characterIndex, int characterType) { 
            File.Delete(GetPath(Guid, characterIndex)); 
            return StaticData.GetCharacterBaseValues(characterType).ToByte(); 
        }

        public static CustomizedCharacter getCharacterData(int Guid, int characterGuid, int characterIndex) {
            string path = GetPath(Guid, characterGuid);
            return File.Exists(path) ? new(File.ReadAllBytes(path)) : StaticData.GetCharacterBaseValues(characterIndex);
        }

        public static string GetPath(int Guid, int characterIndex) {
            string path = "C:\\Users\\Klauke\\Documents\\My Games\\Corivi\\GameServer\\CharacterData " + Guid.ToString() + "\\";
            ServerHelper.EnsureFolderExists(path);
            path += characterIndex.ToString();
            return path;
        }

        private static byte CalcAllocatedPoints(byte[] deltaStats) { 
            byte statIncrease = 0;
            byte statDecrease = 0;
            for (int i = 0; i < 4; i++) { 
                statIncrease += (byte)Math.Max((sbyte)deltaStats[i], (byte)0);
                statDecrease += (byte)Math.Max(-(sbyte)deltaStats[i], (byte)0);
            }
            return statIncrease == statDecrease ? statIncrease : (byte)100;
        } 

        public static byte[] AddStats(byte[] b1, byte[] b2) { 
            for (int i = 0; i < 4; i++) 
                b1[i] += b2[i];
             
            return b1;
        }
    }
}
