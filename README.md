# SF Memory Binary Maker

![N|Solid](https://preview.ibb.co/nwTqh9/SF_Memory_Cassette_and_GB_Memory_Cartridge.jpg)

##### This program's main purpose is to generate binaries to burn to a SF Memory Cartridge 

 It is able to:
 - Generate a 4096kB binary including the standard SF Memory Menu(512kB) and as many smaller ROMs as the space allows
+ a fitting MAP file to go with the binary
 - Generate a MAP file to burn a single ROM without menu to your cartridge. This is helpful if you want to use your SF Memory cart with a ROM that is 4096kB on it's own. It does also work with ROMs smaller than that though.
  
Please keep in mind that to generate a binary with the Menu, the program requires the 512kB menu binary (named Menu.sfc) in the program folder!

![N|Solid](https://preview.ibb.co/k62xvU/MAR_CONTR.png )
___
### To read/write from and to your SF Memory cartridge I highly recommend using Sanni's [Cartridge Reader Shield for Arduino Mega 2560](https://github.com/sanni/cartreader)
Please join the discussion http://forum.arduino.cc/index.php?topic=158974

If You want to order assembled cartreader, please contact me through forum.arduino.cc my nikname there: moldov

Thanks to:

sanni - for his masterpiece which gathered all the community solutions for scattered ROMS and platforms

skaman - SNES rom and mapping details 

infinest - help with GB Memory Binary Maker source code which I forked and manage to adopt for SNES platform

alex_n00b - bitmap letters for Menu

and all comunity's creative work which allows to find those hidden gems and undocumented abilities in retro platforms