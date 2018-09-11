using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SF_Memory
{
    public class ROM
    {
        public String Title, GameCode, ASCIITitle,File;
        public Byte CartridgeType, ROMSize, RAMSize;
        public bool padded,CGB,embedded = false;
        public uint ROMPos = 0;
        public long ROMFileSize;

        public int ROMSizeKByte
        {
            get
            {
                return (0x1 << ROMSize);
            }
        }

        public Int16 ROMFileSizeKByte
        {
            get
            {
                return (Convert.ToInt16(ROMFileSize / 1024));
            }
        }



        public int RAMSizeKByte

        {
            get
            {
                if (RAMSize != 0x00) {
                    return (0x1 << RAMSize);
                }
                return 0x00 ;
            }
        }
        public String CartridgeTypeString
        {
            get
            {
                /*  $xFD5	ROM makeup byte.	xxAAxxxB; AA==11 means FastROM ($30). If B is set, it's HiROM, otherwise it's LoROM.
                    $xFD6 ROM type.ROM / RAM / SRAM / DSP1 / FX
                */
                switch (CartridgeType)
                {
                    case (0x20):
                        return "LoROM";
                    case (0x21):
                        return "HiROM";
                    case (0x23):
                        return "SA-1 ROM";
                    case (0x30):
                        return "LoROM + FastROM";
                    case (0x31):
                        return "HiROM + FastROM";
                    case (0x32):
                        return "ExLoROM";
                    case (0x35):
                        return "ExHiROM";
                    }
                return "Unsupported - Probably HiROM?";
            }
        }
    }
}
