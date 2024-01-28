using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Server {
    internal class CharacterDB {
        public const string FilesPath = "C:\\Users\\Klauke\\Documents\\My Games\\Corivi\\GameServer\\";

        public static byte[] getCharacterData(int Guid, int itemIndex, int characterIndex) {

            string path = FilesPath + "CharacterData " + Guid.ToString() + "\\" ; 
            ServerHelper.EnsureFolderExists(path);
            path += itemIndex.ToString();

            byte[] result; 
            if (File.Exists(path)) {
                result = File.ReadAllBytes(path);
            } else { 
                result = StaticData.GetCharacterBaseValues(characterIndex).ToByte();  //Get base values
            }

            return result;
        }

        public static byte[] AddStats(byte[] b1, byte[] b2) {

            for (int i = 0; i < 4; i++) {
                b1[i] += b2[i];
            }
            return b1;
        }

        public static async Task<byte[]> saveCharacterData(int Guid, int characterIndex, byte[] data, MainWindow main) {

            string path = FilesPath + "CharacterData " + Guid.ToString() + "\\"; 
            ServerHelper.EnsureFolderExists(path); 
            path += characterIndex.ToString();
             
            CustomizedCharacter ClientRequestedCharSave = new(data);
            ClientRequestedCharSave.statpointsFullyAllocated = CalcAllocatedPoints(ClientRequestedCharSave.stats);
            int characterType = await main.getItemID(Guid, characterIndex);
            CustomizedCharacter currentCharacter = File.Exists(path) ? new(File.ReadAllBytes(path)) : StaticData.GetCharacterBaseValues(characterType);
             
            byte[] stats = new byte[4];
            Buffer.BlockCopy(data, 0, stats, 0, 4);//stats are sent as delta, not as absolute
            int characterXP = await main.getItemXP(characterIndex, Guid);
            int characterLevel = (int)Math.Sqrt(characterXP / 10);

            int statsLeftToAllocate = (characterLevel*2) - currentCharacter.statpointsFullyAllocated;
            if (ClientRequestedCharSave.statpointsFullyAllocated > statsLeftToAllocate) {
                return currentCharacter.ToByte();
            }
            ClientRequestedCharSave.stats = AddStats(ClientRequestedCharSave.stats, currentCharacter.stats);
            ClientRequestedCharSave.statpointsFullyAllocated += currentCharacter.statpointsFullyAllocated;
            byte[] dataToSave = ClientRequestedCharSave.ToByte();
            File.WriteAllBytes(path, dataToSave);
            return dataToSave;
        }

        public static async Task<byte[]> ResetStats(int Guid, int characterIndex, int characterType) {

            string path = FilesPath + "CharacterData " + Guid.ToString() + "\\";
            ServerHelper.EnsureFolderExists(path);
            path += characterIndex.ToString();
              
            byte[] currentCharacter =  StaticData.GetCharacterBaseValues(characterType).ToByte();

            File.WriteAllBytes(path, currentCharacter);
            return currentCharacter; 
        }

        private static byte CalcAllocatedPoints(byte[] deltaStats) {
              
            byte totalDeltaStatsPositive = 0;
            byte totalDeltaStatsNegative = 0;
            for (int i = 0; i < 4; i++) { 
                totalDeltaStatsPositive += (byte)Math.Max((sbyte)deltaStats[i], (byte)0);
                totalDeltaStatsNegative += (byte)Math.Max(-(sbyte)deltaStats[i], (byte)0);
            }
            if (totalDeltaStatsPositive != totalDeltaStatsNegative)
                return 100; //return absurdly high value to prevent server from serving

            return totalDeltaStatsPositive;
        }

        public static int CharacterLevelDifference(ref CustomizedCharacter c1, ref CustomizedCharacter c2) {
            int diff = 0;
            for (int i = 0; i < 4; i++) {
                diff += Math.Abs(c1.stats[i] - c2.stats[i]);
            }
            if (diff%2 != 0) {
                return Int32.MaxValue;// since this returns how much lvl is needed, an uneven number is impossible to save
            }

            return diff/2;
        }
    }
}
