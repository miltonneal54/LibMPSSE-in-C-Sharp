# LibMPSSE-in-C-Sharp
Wrapper Library for FTDI's  libMSSPE Version 1.0.5

FTDI's libMPSSE.dll allows interfacing with FT232H, FT2232H and FT4232H based modules.

For this project I used:

FT232H breakout single channel module.

24C02P (2048 bit) I2C EEPROM.

CAT93C46 (1024 bit) SPI EEPROM.

The Library consists of 3 main classes.

MPSSE (Base class)
MPSSE_I2C (Access to the I2C methods)
MPSSE_SPI (Access to the SPI methods)
The test projects are based on the C source in FTDI Application Notes AN177 (I2C) and AN178 (SPI). My test devices are a smaller in capacity than used in the original source, so the code varies a little from FTDI's sample code.

Descrepancies:

I did find some bugs in the original C source in AN177 and AN178 which I corrected when ported the code to C#.

Prerequisites:

The FTDI's driver FTD2xx must be installed on your system.
