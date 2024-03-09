
using System.IO; 

namespace Server {
    internal static class StaticData {

        public static void BuildBaseValues() { //create the files that we will network from the baseValues Array
            int i = 0;
            string path = ServerHelper.BasePath + "CharacterBaseValues\\";

            for (int g = 0; g < baseValues.Length; g++) {
                File.WriteAllBytes(path+g.ToString()+".characterData", baseValues[g].ToByte());
            }
        }

        static CustomizedCharacter[] baseValues = {
            new CustomizedCharacter(
                new byte[] {1, 1, 1, 1},
                new byte[] {0, 0, 0, 0},
                new byte[] {0,0,0,0,0,0,0,0,0,0},
                0,
                0
                ),            
            
            new CustomizedCharacter(
                new byte[] {10, 15, 15, 20},
                new byte[] {0, 1, 2, 1},
                new byte[] {0,0,0,0,0,0,0,0,0,0},
                0,
                0
                ),

            new CustomizedCharacter(
                new byte[] {20, 15, 15, 10},
                new byte[] {0, 1, 2, 1},
                new byte[] {2,3,1,0,0,0,0,0,0,0},
                0,
                0
                ), 

            new CustomizedCharacter(
                new byte[] {20, 18, 12, 10},
                new byte[] {0, 1, 2, 1},
                new byte[] {1,0,0,5,0,0,2,0,0,0},
                0,
                0
                )

        };

        public static CustomizedCharacter GetCharacterBaseValues(int index) {

            index -= 1000; //character itemIDs start at 1001
            //TODO, make a constructor for base values of chustomized character, make an array of these thigs in this class 
            if (index < 1 || index > baseValues.Length)
                index = 0; //i.e. return  0 0 0 

            return baseValues[index];
        }


        public static int CompareToBase() { 
            return 0;
        }
    }
}
