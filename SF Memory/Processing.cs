using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SF_Memory
{
    public static class Processing
    {
        

        public static ROM ParseROM(MemoryStream Data, String File = null)
        {
            ROM Output = new ROM();
            Output.ROMFileSize = Data.Length;

            DialogResult dialogResult = MessageBox.Show("Press yes for hiROM and no for loROM", "Select ROM type", MessageBoxButtons.YesNo);    
            if (dialogResult == DialogResult.Yes)    
            {    
                Data.Position = 0xFFD5;    
            }    
            else if (dialogResult == DialogResult.No)    
            {    
                Data.Position = 0x7FD5;    
            }   
            Output.CartridgeType = (byte)Data.ReadByte();

            //Check memory mapping type
            if (Output.CartridgeType % 2 > 0 )
            {
                //HIROM
                Data.Position = 0xFFD7;
            }
            else
            {
                //LOWROM
                Data.Position = 0x7FD7;
            }

            //Read ROM and RAM size
            Output.ROMSize = (byte)Data.ReadByte();
            Output.RAMSize = (byte)Data.ReadByte();

            //Read ROM header data
            Data.Position = Data.Position - 0x19;
            Byte[] buffer = new Byte[22];
            Data.Read(buffer, 0, 22);
            Output.ASCIITitle = System.Text.ASCIIEncoding.ASCII.GetString(buffer);
            Data.Position = Data.Position - 0x25;
            Data.Read(buffer, 0, 0x10);
            Output.GameCode = System.Text.ASCIIEncoding.ASCII.GetString(buffer);


            if (Output.ROMSizeKByte < 128)
            {
                Output.padded = true;
            }
            if (!String.IsNullOrEmpty(File))
            {
                Output.File = File;
            }
            Data.Dispose();
            return Output;
        }

        public static List<ROM> ParseSFMBinary(String ToImport)
        {
            List<ROM> ROMsToAdd;
            using (FileStream Reader = new FileStream(ToImport, FileMode.Open, FileAccess.Read))
            {
                Reader.Position = 0x7FB0;
                Byte[] temp = new Byte[6];

                //ROM ASCII title
                Reader.Read(temp, 0, 6);

                ROMsToAdd = new List<ROM>();
                if (Encoding.ASCII.GetString(temp) != "01MENU")
                {
                    MessageBox.Show(String.Format("File {1}{0}{1} is not a valid SFM binary", Path.GetFileName(ToImport), '"'), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return ROMsToAdd;
                }
                Reader.Position = 0x62000;
                Byte NextGameIndex = (Byte)Reader.ReadByte();
                ROM Temp = new ROM();
                //TitleEntry ROMTitle;

                //Read info for each game written to the menu binary into ROM class and add all to the ROMsToAdd list until end is reached
                while (NextGameIndex != 0 && NextGameIndex != 0xFF)
                {
                    int Base = (Reader.ReadByte() - 1) * 128;
                    Reader.Position = 0x20000 + (Base * 1024) + 0x148;
                    int Size = (32 << Reader.ReadByte());
                    Reader.Position = 0x20000 + Base * 1024;
                    temp = new Byte[Size * 1024];
                    Reader.Read(temp, 0, Size * 1024);
                    using (MemoryStream Mem = new MemoryStream(temp))
                    {
                        Temp = ParseROM(Mem);
                    }
                    Temp.ROMPos = (uint)(0x20000 + Base * 1024);
                    Temp.embedded = true;
                    Temp.File = ToImport;

                    
                    ROMsToAdd.Add(Temp);
                    Reader.Position = 0x62000 + 0x2000 * ROMsToAdd.Count;
                    NextGameIndex = (Byte)Reader.ReadByte();
                    Temp = new ROM();
                }
            }
            return ROMsToAdd;
        }

        public static Byte[] GenerateStandaloneMAPForROM(ROM ROMToProcess)
        {
            Byte[] MAPBytes = new Byte[0x200];

            using (MemoryStream Mem = new MemoryStream(MAPBytes))
            {
                /*
                There are always 8 bytes at odd addresses at C0FF01..0F, interleaved with the mapping entries 0 and 1(though no matter if the cart uses 1, 2, or 3 mapping entries). The 'odd' bytes are some serial number, apart from the first two bytes, it seems to be just a BCD date / time stamp, ie.formatted as 11 - xx - YY - MM - DD - HH - MM - SS.
                New findings are that the "xx" in the "11-xx-YY-MM-DD-HH-MM-SS" can be non - BCD(spotted in the Super Puyo Puyo cart).
                */

                Byte[] mappingBuffer = new Byte[512];
                if (File.Exists(Application.StartupPath + @"\mapping.map"))
                {
                    mappingBuffer = File.ReadAllBytes(Application.StartupPath + @"\mapping.map");
                }

                /*
                Bit0-1 SRAM Size (0=2K, 1=8K, 2=32K, 3=None) ;ie. 2K SHL (N2)
                Bit2-4 ROM Size (0=512K, 2=1.5M, 5=3M, 7=4M) ;ie. 512K(N+1)
                Bit5 Zero (maybe MSB of ROM Size for carts with three FLASH chips) (set for HIROM:ALL)
                Bit6-7 Mode (0=Lorom, 1=Hirom, 2=Forced HIROM:MENU, 3=Forced HIROM:ALL)
                More info: http://problemkaputt.de/fullsnes.htm#snescartnintendopowerflashcard

                Example:
                0x5d = 0b 01 0 111 01
                01 -> Hirom
                0
                111 -> 4M
                01 -> 8K
                */

                int ROMBits = 0x00;
                    int SRAMBits = 0x00;

                    ROMBits = ((ROMToProcess.ROMFileSizeKByte - 512) / 512) * 4; //SHIFT_LEFT_2_TIMES<<
                    SRAMBits = ROMToProcess.RAMSizeKByte;

                    //Mapping type Type
                    if (ROMToProcess.CartridgeType % 2 >= 0x1)
                    {
                        //HiRom
                        ROMBits = ROMBits + 0x40; //RISE_HIROM_BIT
                    }

                    //SRAM Size
                    /*Bit0-1 SRAM Size (0=2K, 1=8K, 2=32K, 3=None) ;ie. 2K SHL (N2)*/
                    if (ROMToProcess.RAMSizeKByte == 2)
                    {
                        //2K
                        //ROMBits = ROMBits;
                    }
                    else if (ROMToProcess.RAMSizeKByte == 8)
                    {
                        //8K
                        ROMBits = ROMBits + 0b01; //RISE_8K_SRAM_BIT
                    }
                    else if (ROMToProcess.RAMSizeKByte == 32)
                    {
                        //32K
                        ROMBits = ROMBits + 0b10; //RISE_32K_SRAM_BIT
                    }

                    else
                    {
                        //None
                        ROMBits = ROMBits + 0b11; //RISE_NONE_BIT
                    }


               
                    Mem.Write(mappingBuffer, 0, 512);
                    Mem.Position = 0x00;
                    Mem.WriteByte(Convert.ToByte(ROMBits));



                /*Port 2404h = Size (R)
                0-1 SRAM Size (0=2K, 1=8K, 2=32K, 3=None) ;ie. 2K SHL (N*2)
                2-4 ROM Size (0=512K, 2=1.5M, 5=3M, 7=4M) ;ie. 512K*(N+1)
                5   Maybe ROM Size MSB for carts with three FLASH chips (set for HIROM:ALL)
                6-7 Mode (0=Lorom, 1=Hirom, 2=Forced HIROM:MENU, 3=Forced HIROM:ALL)

              Port 2407h = Base (R)
                0-3 SRAM Base in 2K units
                4-7 ROM Base in 512K units (bit7 set for HIROM:MENU on skaman's blank cart)

              Port 2405h,2406h = SRAM Mapping Related (R)
              The values for port 2405h/2406h are always one of these three sets, apparently related to SRAM mapping:
                29,4A for Lorom with SRAM
                61,A5 for Hirom with SRAM
                AA,AA for Lorom/Hirom without SRAM
                61,A5 (when forcing HIROM:ALL)
                D5,7F (when forcing HIROM:MENU)
                8A,8A (when forcing HIROM:MENU on skaman's blank cart)
              Probably selecting which bank(s) SRAM is mapped/mirrored in the SNES memory space.*/
                     Mem.Position = 0x02;
                    if (ROMToProcess.RAMSizeKByte != 0)
                    {
                        if (ROMToProcess.CartridgeType % 2 > 0)
                        {
                            //HighROM 61,A5 for Hirom with SRAM
                            Mem.WriteByte(0x61);
                            Mem.Position = 0x04;
                            Mem.WriteByte(0xA5);

                        }
                        else
                        {
                            //LowROM 29,4A for Lorom with SRAM
                            Mem.WriteByte(0x29);
                            Mem.Position = 0x04;
                            Mem.WriteByte(0x4A);
                            
                        }
                    }
                    else
                    {
                        //AA,AA for Lorom / Hirom without SRAM
                        Mem.WriteByte(0xAA);
                        Mem.Position = 0x04;
                        Mem.WriteByte(0xAA);
                        
                    }

                /*
                There are always 8 bytes at odd addresses at C0FF01..0F, interleaved with the mapping entries 0 and 1(though no matter if the cart uses 1, 2, or 3 mapping entries). The 'odd' bytes are some serial number, apart from the first two bytes, it seems to be just a BCD date / time stamp, ie.formatted as 11 - xx - YY - MM - DD - HH - MM - SS.
                New findings are that the "xx" in the "11-xx-YY-MM-DD-HH-MM-SS" can be non - BCD(spotted in the Super Puyo Puyo cart).
                */

                //01BFh 10   Date "MM/DD/YYYY"(or "YYYY/MM/DD" on "NINnnnnn" carts)
                //1st 4 bytes of date are written so need already 
                //Date to BCD

                DateTime date = DateTime.Now;
                Mem.Position = 0x05;
                Mem.WriteByte(to_bcd((date.Year - 2000)));
                Mem.Position = 0x07;
                Mem.WriteByte(to_bcd(date.Month));
                Mem.Position = 0x09;
                Mem.WriteByte(to_bcd(date.Day));
                Mem.Position = 0x0B;
                Mem.WriteByte(to_bcd(date.Hour));
                Mem.Position = 0x0D;
                Mem.WriteByte(to_bcd(date.Minute));
                Mem.Position = 0x0F;
                Mem.WriteByte((byte)(to_bcd(date.Second)));

                //Convert Date to BCD
                byte to_bcd(int n)
                {
                    // extract each digit from the input number n
                    byte d1 = (byte)(n / 10);
                    byte d2 = (byte)(n % 10);
                    // combine the decimal digits into a BCD number
                    return (byte)((d1 << 4) | d2);
                }

            }
            return MAPBytes;

            /*

                //Title SHIFT-JIS
                //temp = new Byte[] { 0x82, 0x50, 0x82, 0x54, 0x82, 0x61, 0x83, 0x7B, 0x83, 0x93, 0x83, 0x6F, 0x81, 0x5B, 0x83, 0x7D, 0x83, 0x93, 0x82, 0x66, 0x82, 0x61, 0x82, 0x52, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20 };
                Encoding ShiftJIS = Encoding.GetEncoding(932);
                String temp1 = "これを読めば、あなたはばかだ";
                temp1 = temp1.PadRight(30, ' ');
                temp = ShiftJIS.GetBytes(temp1);
                Mem.Write(temp, 0, temp.Length);

                //Date ASCII "MM/DD/YYYY" + Time ASCII"HH:MM:SS"
                temp1 = DateTime.Now.ToString(@"MM\/dd\/yyyyHH:mm:ss");
                temp = System.Text.ASCIIEncoding.ASCII.GetBytes(temp1);
                Mem.Write(temp, 0, temp.Length);

                //LAW ASCII  "LAWnnnnn"
                temp = System.Text.ASCIIEncoding.ASCII.GetBytes("LAW03347");
                Mem.Write(temp, 0, temp.Length);
                

                //?? No idea what this does
                //temp = new Byte[] { 0x01, 0x00, 0x30, 0x25, 0x00, 0x03, 0x01, 0x00, 0x12, 0x57, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00 };
                //Mem.Write(temp, 0, temp.Length);
            */
        }

        public static Byte[] GenerateMAPForMenuBinary(List<ROM> ROMList)
        {
            Byte[] MAPBytes = new Byte[0x200];
            using (MemoryStream Mem = new MemoryStream(MAPBytes))
            {
                int SRAMBaseBits = 0x00;
                int ROMBaseBits = 0x01;

                /*
                There are always 8 bytes at odd addresses at C0FF01..0F, interleaved with the mapping entries 0 and 1(though no matter if the cart uses 1, 2, or 3 mapping entries). The 'odd' bytes are some serial number, apart from the first two bytes, it seems to be just a BCD date / time stamp, ie.formatted as 11 - xx - YY - MM - DD - HH - MM - SS.
                New findings are that the "xx" in the "11-xx-YY-MM-DD-HH-MM-SS" can be non - BCD(spotted in the Super Puyo Puyo cart).
                */
                //MENU 8 BYTES INTERLEAVED WITH "11-xx-YY-MM"
                Byte[] temp = new Byte[] { 0x03, 0x11, 0xAA, 0x74, 0xAA, 0x97, 0x00, 0x12 };
                Mem.Write(temp, 0, temp.Length);

                //Write MBC Type, ROM Size, and SRAM Size etc. for all ROMs
                for (int i = 0; i < ROMList.Count; i++)
                {
                    //Write MBC Type, ROM Size, and SRAM Size etc. for ROM
                    
                    /*
                    Bit0-1 SRAM Size (0=2K, 1=8K, 2=32K, 3=None) ;ie. 2K SHL (N2)
                    Bit2-4 ROM Size (0=512K, 2=1.5M, 5=3M, 7=4M) ;ie. 512K(N+1)
                    Bit5 Zero (maybe MSB of ROM Size for carts with three FLASH chips) (set for HIROM:ALL)
                    Bit6-7 Mode (0=Lorom, 1=Hirom, 2=Forced HIROM:MENU, 3=Forced HIROM:ALL)
                    More info: http://problemkaputt.de/fullsnes.htm#snescartnintendopowerflashcard

                    Example:
                    0x5d = 0b 01 0 111 01
                    01 -> Hirom
                    0
                    111 -> 4M
                    01 -> 8K
                    */

                    int ROMBits = 0x00;
                    int SRAMBits = 0x00;
                    
                    ROMBits = ((ROMList[i].ROMFileSizeKByte - 512) / 512) * 4; //SHIFT_LEFT_2_TIMES<<
                    SRAMBits = ROMList[i].RAMSizeKByte;

                    //Mapping type Type
                    if (ROMList[i].CartridgeType % 2 >= 0x1)
                    {
                        //HighRom
                        ROMBits = ROMBits + 0x40; //RISE_HIROM_BIT
                    }

                    //SRAM Size
                    /*Bit0-1 SRAM Size (0=2K, 1=8K, 2=32K, 3=None) ;ie. 2K SHL (N2)*/
                    if (ROMList[i].RAMSizeKByte == 2)
                    {
                        //2K
                        //ROMBits = ROMBits;
                    }
                    else if (ROMList[i].RAMSizeKByte == 8)
                    {
                        //8K
                        ROMBits = ROMBits + 0b01; //RISE_8K_SRAM_BIT
                    }
                    else if (ROMList[i].RAMSizeKByte == 32)
                    {
                        //32K
                        ROMBits = ROMBits + 0b10; //RISE_32K_SRAM_BIT
                    }

                    else
                    {
                        //None
                        ROMBits = ROMBits + 0b11; //RISE_NONE_BIT
                    }

                    
                    //ROM Startoffset
                    Byte StartOffsetByte = (byte)(i *0x08 + 0x08);

                    Mem.Position = StartOffsetByte;
                    Mem.WriteByte(Convert.ToByte(ROMBits));
                   


                    /*Port 2404h = Size (R)
                    0-1 SRAM Size (0=2K, 1=8K, 2=32K, 3=None) ;ie. 2K SHL (N*2)
                    2-4 ROM Size (0=512K, 2=1.5M, 5=3M, 7=4M) ;ie. 512K*(N+1)
                    5   Maybe ROM Size MSB for carts with three FLASH chips (set for HIROM:ALL)
                    6-7 Mode (0=Lorom, 1=Hirom, 2=Forced HIROM:MENU, 3=Forced HIROM:ALL)

                    Port 2407h = Base (R)
                    0-3 SRAM Base in 2K units
                    4-7 ROM Base in 512K units (bit7 set for HIROM:MENU on skaman's blank cart)

                    Port 2405h,2406h = SRAM Mapping Related (R)
                    The values for port 2405h/2406h are always one of these three sets, apparently related to SRAM mapping:
                    29,4A for Lorom with SRAM
                    61,A5 for Hirom with SRAM
                    AA,AA for Lorom/Hirom without SRAM
                    61,A5 (when forcing HIROM:ALL)
                    D5,7F (when forcing HIROM:MENU)
                    8A,8A (when forcing HIROM:MENU on skaman's blank cart)
                    Probably selecting which bank(s) SRAM is mapped/mirrored in the SNES memory space.*/
                    Mem.WriteByte(0xFF);

                    if (ROMList[i].RAMSizeKByte !=0)
                    {
                        if (ROMList[i].CartridgeType  % 2 > 0 )
                        {
                            //HighROM 61,A5 for Hirom with SRAM
                            Mem.WriteByte(0x61);
                            Mem.WriteByte(0xFF);
                            Mem.WriteByte(0xA5);
                            Mem.WriteByte(0xFF);
                        }
                        else
                        {
                            //LowROM 29,4A for Lorom with SRAM
                            Mem.WriteByte(0x29);
                            Mem.WriteByte(0xFF);
                            Mem.WriteByte(0x4A);
                            Mem.WriteByte(0xFF);
                        }
                    }
                    else
                    {
                        //AA,AA for Lorom / Hirom without SRAM
                        Mem.WriteByte(0xAA);
                        Mem.WriteByte(0xFF);
                        Mem.WriteByte(0xAA);
                        Mem.WriteByte(0xFF);
                    }

                    //Write 14 byte which means (15 byte is a high part)
                    //Port 2407h = Base (R)
                    //0 - 3 SRAM Base in 2K units
                    //4 - 7 ROM Base in 512K units(bit7 set for HIROM:MENU on skaman's blank cart)

                    if (i == 0)
                    {
                        Mem.WriteByte(0x10);
                    }
                    else
                    {
                        SRAMBaseBits = SRAMBaseBits + ROMList[i-1].RAMSizeKByte / 2;
                        ROMBaseBits = ROMBaseBits + ROMList[i-1].ROMFileSizeKByte / 512;

                        int temp3 = ROMBaseBits * 16; //SHIFTING LEFT BY 4 DIGITS <<
                        temp3 = temp3 + SRAMBaseBits;
                        Mem.WriteByte(Convert.ToByte(temp3));
                    }
                }

                while (Mem.Position < 0x192)
                {
                    Mem.WriteByte(0xFF);
                }
                temp = new Byte[] { 0x55, 0x00 };
                Mem.Write(temp, 0, temp.Length);
                while (Mem.Position < 0x1A6)
                {
                    Mem.WriteByte(0xFF);
                }
                temp = new Byte[] { 0x55, 0x00 };
                Mem.Write(temp, 0, temp.Length);
                while (Mem.Position < 0x1B6)
                {
                    Mem.WriteByte(0xFF);
                }
                temp = new Byte[] { 0x55, 0x00 };
                Mem.Write(temp, 0, temp.Length);
                while (Mem.Position < 0x200)
                {
                    Mem.WriteByte(0xFF);
                }

                /*
               There are always 8 bytes at odd addresses at C0FF01..0F, interleaved with the mapping entries 0 and 1(though no matter if the cart uses 1, 2, or 3 mapping entries). The 'odd' bytes are some serial number, apart from the first two bytes, it seems to be just a BCD date / time stamp, ie.formatted as 11 - xx - YY - MM - DD - HH - MM - SS.
               New findings are that the "xx" in the "11-xx-YY-MM-DD-HH-MM-SS" can be non - BCD(spotted in the Super Puyo Puyo cart).
               */

                //Date to BCD
                DateTime date = DateTime.Now;
                Mem.Position = 0x05;
                Mem.WriteByte(to_bcd((date.Year - 2000)));
                Mem.Position = 0x07;
                Mem.WriteByte(to_bcd(date.Month));
                Mem.Position = 0x09;
                Mem.WriteByte(to_bcd(date.Day));
                Mem.Position = 0x0B;
                Mem.WriteByte(to_bcd(date.Hour));
                Mem.Position = 0x0D;
                Mem.WriteByte(to_bcd(date.Minute));
                Mem.Position = 0x0F;
                Mem.WriteByte((byte)(to_bcd(date.Second)));

                //Convert Date to BCD
                byte to_bcd(int n)
                {
                    // extract each digit from the input number n
                    byte d1 = (byte)(n / 10);
                    byte d2 = (byte)(n % 10);
                    // combine the decimal digits into a BCD number
                    return (byte)((d1 << 4) | d2);
                }
            }

            return MAPBytes;
        }
        public static void CreateMenuBinary(List<ROM> ROMList, ref Byte[] Template)
        {
            using (MemoryStream Mem = new MemoryStream(Template))
            {

                //Write Menu to ROM
                Byte[] MenuHeaderBuffer = new Byte[480];
                if (File.Exists(Application.StartupPath + @"\Menu1"))
                {
                    MenuHeaderBuffer = File.ReadAllBytes(Application.StartupPath + @"\Menu1");
                }

                Mem.Position = 0x60000;
                Mem.Write(MenuHeaderBuffer, 0, MenuHeaderBuffer.Length);
                          

                Mem.Position = 0x62000;

                int ROMBlock = 0x01;
                int totalROMSize = 0x00;
                int RAMBlock = 0x00;
                int totalRAMSize = 0x00;

                Byte[] array1 = new Byte[5];


                for (int i = 0; i < ROMList.Count; i++)
                {

                    /*
                    * 
                    * h 1    Directory index (00h..07h for Entry 0..7) (or FFh=Unused Entry)
                     0001h 1    First 512K-FLASH block (00h..07h for block 0..7)
                     0002h 1    First 2K-SRAM block    (00h..0Fh for block 0..15)
                     0003h 2    Number of 512K-FLASH blocks (mul 4) (=0004h..001Ch for 1..7 blks)
                     0005h 2    Number of 2K-SRAM blocks (mul 16)   (=0000h..0100h for 0..16 blks)
                     0007h 12   Gamecode (eg. "SHVC-MENU-  ", "SHVC-AGPJ-  ", or "SHVC-CS  -  ")
                     0013h 44   Title in Shift-JIS format (padded with 00h's) (not used by Menu)
                     003Fh 384  Title Bitmap (192x12 pixels, in 30h*8 bytes, ie. 180h bytes)
                     01BFh 10   Date "MM/DD/YYYY" (or "YYYY/MM/DD" on "NINnnnnn" carts)
                     01C9h 8    Time "HH:MM:SS"
                     01D1h 8    Law  "LAWnnnnn" or "NINnnnnn" (eg. "LAW01712", or "NIN11001")
                     01D9h 7703 Unused (1E17h bytes, FFh-filled)
                     1FF0h 16   For File0: "MULTICASSETTE 32" / For Files 1-7: Unused (FFh-filled)
                     */

                    //ROM Index
                    Mem.WriteByte((Byte)(i+1));
                    

                    ROMBlock = ROMBlock + totalROMSize;
                    Mem.WriteByte((Byte)(ROMBlock));
                    //0002h 1    First 2K-SRAM block    (00h..0Fh for block 0..15)

                    //RAMBlock = RAMBlock + totalRAMSize;
                    RAMBlock = RAMBlock + totalRAMSize;
                    Mem.WriteByte((Byte)(RAMBlock));



                    //0003h 2    Number of 512K - FLASH blocks(mul 4)(= 0004h..001Ch for 1..7 blks)
                    totalROMSize = totalROMSize + Convert.ToByte(ROMList[i].ROMFileSizeKByte / 512);
                    Mem.WriteByte((Byte)(ROMList[i].ROMFileSizeKByte / 0x80));
                    Mem.WriteByte(0x00);
                    
                    //0005h 2    Number of 2K-SRAM blocks (mul 16)   (=0000h..0100h for 0..16 blks)
                    Mem.WriteByte((Byte)(ROMList[i].RAMSizeKByte / 2 * 16));
                    totalRAMSize = totalRAMSize + (ROMList[i].RAMSizeKByte / 2);
                    Mem.WriteByte(0x00);
                    
                    // 0007h 12   Gamecode (eg. "SHVC-MENU-  ", "SHVC-AGPJ-  ", or "SHVC-CS  -  ")
                    Byte[] temp = new Byte[0];
                    temp = System.Text.ASCIIEncoding.ASCII.GetBytes("SHVC-" + ROMList[i].GameCode.Substring(2, 4) + "-  ");
                    Mem.Write(temp, 0, temp.Length);



                    //Title SHIFT-JIS
                    //0013h 44   Title in Shift - JIS format(padded with 00h's) (not used by Menu)
                    /*String temp1;
                    Encoding ShiftJIS = Encoding.GetEncoding(932);
                    String temp7 = "これを読めば、あなたはばかだ";
                    temp1 = temp7.PadRight(88);
                    //CHECK_WHAT_TO_DO_WITH_JAPANESE_NAME
                    temp = ShiftJIS.GetBytes(temp7);
                    Mem.Write(temp, 0, temp.Length);
                    */
                    for (int b = 0; b < 44; b++)
                    {
                        Mem.WriteByte(0x00);
                    }

                    
                    //Create Bitmap for game name for Menu
                    String temp2 = ROMList[i].Title.PadRight(16);
                    Byte[] LetterBuffer;
                    
                        for (int c = 0; c < temp2.Length; c++)
                        {
                            if (File.Exists(Application.StartupPath + @"\letters\" + temp2[c]))
                            {
                                LetterBuffer = File.ReadAllBytes(Application.StartupPath + @"\letters\" + temp2[c]);
                            }
                            else
                            {
                                LetterBuffer = File.ReadAllBytes(Application.StartupPath + @"\letters\space");
                            }
                            Mem.Write(LetterBuffer, 0, 24);
                        }
                    
                    //01BFh 10   Date "MM/DD/YYYY"(or "YYYY/MM/DD" on "NINnnnnn" carts)
                    String temp1 = DateTime.Now.ToString(@"MM\/dd\/yyyyHH:mm:ss");
                    temp = System.Text.ASCIIEncoding.ASCII.GetBytes(temp1);
                    Mem.Write(temp, 0, temp.Length);

                    //LAW ASCII  "LAWnnnnn"
                    //01D1h 8    Law  "LAWnnnnn" or "NINnnnnn"(eg. "LAW01712", or "NIN11001")
                    temp = System.Text.ASCIIEncoding.ASCII.GetBytes("LAW01780");
                    Mem.Write(temp, 0, temp.Length);

                    //Unused (FFh-filled) 01D9h 7703 Unused (1E17h bytes, FFh-filled)
                    for (int b = 0; b < 0x1E17; b++)
                    {
                        Mem.WriteByte(0xFF);
                    }

                    //Unused (FFh-filled)(game entries) or "MULTICASSETTE 32"(menu entry)
                    //1FF0h 16   For File0: "MULTICASSETTE 32" / For Files 1 - 7: Unused(FFh - filled)
                    for (int b = 0; b < 0x10; b++)
                    {
                        Mem.WriteByte(0xFF);
                    }
                }
                
                //Write Game ROMs to Binary
                Mem.Position = 0x80000;
                long pos = 0;
                for (int i = 0; i < ROMList.Count; i++)
                {
                    pos = Mem.Position;
                    Byte[] temp = new Byte[0];
                    if (!ROMList[i].embedded)
                    {
                        temp = File.ReadAllBytes(ROMList[i].File);
                    }
                    else
                    {
                        using (FileStream Reader = new FileStream(ROMList[i].File, FileMode.Open, FileAccess.Read))
                        {
                            Reader.Position = ROMList[i].ROMPos;
                            temp = new Byte[ROMList[i].ROMSizeKByte * 1024];
                            Reader.Read(temp, 0, ROMList[i].ROMFileSizeKByte * 1024);
                        }
                    }
                    Mem.Write(temp, 0, ROMList[i].ROMFileSizeKByte * 1024);
                    
                }
                
            }
        }

    }
}
