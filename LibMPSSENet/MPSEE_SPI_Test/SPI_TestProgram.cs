using System;
using MPSSENet;

namespace MPSSE_SPITest
{
    /// <summary>
    /// Wrapper library test application.
    /// Test uses FT232H breakout module in SPI mode.
    /// CAT93C46P 1024 bit (128 byte) EEPROM.
    /// Using the eeprom in 16bit memory mode (ORG pin pulled high)
    /// </summary>
    class SPITestProgram
    {
        // Program constants.
        public const uint deviceBufferSize = 256;
        public const byte writeRetries = 5;
        public const byte retries = 10;
        public static byte dataOffset = 0xd0;
        public const byte addressSize = 6;          // 6 for CAT93C46P :: 8 for CAT93C56P :: 7 for CAT93C57P, CAT35C102
        public const byte commandSize = 9;          // 9 for CAT93C46P :: 11 for CAT93C56P :: 10 for CAT93C57P, CAT35C102

        // Global variables.
        public static MPSSE_SPI mpsse_spi = new MPSSE_SPI();
        public static byte[] buffer = new byte[deviceBufferSize];
        public static MPSSE.FT_STATUS status;
        public static uint channels = 0;

        static void Main()
        {
            MPSSE_SPI.ChannelConfig channelConfig = new MPSSE_SPI.ChannelConfig();
            MPSSE.FT_DEVICE_INFO_NODE deviceInfo = new MPSSE.FT_DEVICE_INFO_NODE();
            ushort address;
            ushort data;

            try
            {
                status = mpsse_spi.SPI_GetNumChannels(ref channels);
                if (status == MPSSE.FT_STATUS.FT_OK)
                {
                    Console.WriteLine("Number of available SPI channels = {0}\n", channels);
                }
            }

            catch (DllNotFoundException e)
            {
                Console.WriteLine(e.Message);
                _ = Console.ReadKey();
                Environment.Exit(1);
            }

            // Display the channel info.
            for (uint i = 0; i < channels; i++)
            {
                status = mpsse_spi.SPI_GetChannelInfo(i, deviceInfo);
                Console.WriteLine("Information on channel {0}.", i + 1);
                Console.WriteLine("\tFlags: {0}", deviceInfo.Flags);
                Console.WriteLine("\tType: {0}", deviceInfo.Type);
                Console.WriteLine("\tID: {0}", deviceInfo.ID);
                Console.WriteLine("\tLocId: {0}", deviceInfo.LocId);
                Console.WriteLine("\tSerial Number: {0}", deviceInfo.SerialNumber);
                Console.WriteLine("\tDescription: {0}", deviceInfo.Description);
                Console.WriteLine("\tHandle:{0} ", deviceInfo.Handle); // Is 0 unless it is open.
            }

            // Open the the channel.
            status = mpsse_spi.SPI_OpenChannel(0);   // Using channel 1.
            if (status == MPSSE.FT_STATUS.FT_OK)
            {
                IntPtr handle = MPSSE.GetHandle();
                Console.WriteLine("Handle: {0}\tStatus: {1}", handle.ToString("x"), status.ToString());
            }

            // Set the config info and initialise the channel.
            channelConfig.ClockRate = 500000;
            channelConfig.LatencyTimer = 16;
            channelConfig.ConfigOptions = MPSSE_SPI.ConfigOptions.SPI_CONFIG_OPTION_MODE0 | MPSSE_SPI.ConfigOptions.SPI_CONFIG_OPTION_CS_DBUS3;
            channelConfig.Pins = 0x00000000;
            status = mpsse_spi.SPI_InitChannel(channelConfig);

            // Connect a LED to GPIO AC8 and Ground.
            // It will flash 10 tens at 250 msec intervals.
            /*Console.WriteLine("Flashing LED, please wait....");
            for (int x = 0; x < 10; x++)
            {
                mpsse_spi.FT_WriteGPIO(0xff, 0x7f);
                System.Threading.Thread.Sleep(250);
                mpsse_spi.FT_WriteGPIO(0xff, 0xff);
                System.Threading.Thread.Sleep(250);
            }

            mpsse_spi.FT_WriteGPIO(0, 0);*/

            // Write 64 words to the EEPROM.
            for (address = 0; address < 64; address++)
            {
                Console.WriteLine("Writing Address: {0} Data: {1}", address, address + dataOffset);
                status = WriteByte(address, (ushort)(address + dataOffset));
            }

            Console.WriteLine("Write to EEPROM completed, press any key to start read.");
            _ = Console.ReadKey(true);

            for (address = 0; address < 128; address++)
            {
                data = 0;

                status = ReadByte(address, ref data);
                Console.WriteLine("Reading Address: {0} Data: {1}", address, data);
            }

            status = mpsse_spi.SPI_CloseChannel();
            Console.WriteLine("Press any key to exit.");
            _ = Console.ReadKey(true);
        }

        static MPSSE.FT_STATUS WriteByte(ushort address, ushort data)
        {
            


            // Write command EWEN(with CS_High -> CS_Low).
            uint sizeToTransfer = commandSize;
            uint sizeTransfered = 0;
            buffer[0] = 0x9f;   // EWEN command -> binary 10011xxxx (9bits).
            buffer[1] = 0xff;
            status = mpsse_spi.SPI_Write(buffer, sizeToTransfer, ref sizeTransfered,
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BITS |
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_CHIPSELECT_ENABLE |
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_CHIPSELECT_DISABLE);

            // CS_High + Write command.
            sizeToTransfer = 3;
            sizeTransfered = 0;
            buffer[0] = 0xa0;                           // Write command (3bits).
            status = mpsse_spi.SPI_Write(buffer, sizeToTransfer, ref sizeTransfered,
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BITS |
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_CHIPSELECT_ENABLE);

            // Write address.
            sizeToTransfer = addressSize;
            sizeTransfered = 0;
            buffer[0] = (byte)(address << (8 - addressSize));
            status = mpsse_spi.SPI_Write(buffer, sizeToTransfer, ref sizeTransfered,
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BITS);

            // Write 2 byte data + CS_Low.
            sizeToTransfer = 2;
            sizeTransfered = 0;

            buffer[0] = (byte)(data & 0xff);
            buffer[1] = (byte)((data & 0xff00) >> 8);
            status = mpsse_spi.SPI_Write(buffer, sizeToTransfer, ref sizeTransfered,
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BYTES |
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_CHIPSELECT_DISABLE);

            // Strobe Chip Select and wait for DO to go high.
            sizeToTransfer = 0;
            sizeTransfered = 0;
            status = mpsse_spi.SPI_Write(buffer, sizeToTransfer, ref sizeTransfered,
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BITS |
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_CHIPSELECT_ENABLE);

            System.Threading.Thread.Sleep(10);

            /*int retry = 0;
            bool state = true;
            status = mpsse_spi.SPI_IsBusy(ref state);
            while (state && (retry < writeRetries))
            {
                Console.WriteLine("SPI device is busy({0})\n", retry);
                status = mpsse_spi.SPI_IsBusy(ref state);
                retry++;
            }*/

            sizeToTransfer = 0;
            sizeTransfered = 0;
            status = mpsse_spi.SPI_Write(buffer, sizeToTransfer, ref sizeTransfered,
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BITS |
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_CHIPSELECT_DISABLE);
            // Write Disable Command.
            // Write command EWDSN(with CS_High -> CS_Low).
            sizeToTransfer = commandSize;
            sizeTransfered = 0;
            buffer[0] = 0x87;   // EWDS Command -> binary 10000xxxx (9bits)
            buffer[1] = 0xff;
            status = mpsse_spi.SPI_Write(buffer, sizeToTransfer, ref sizeTransfered,
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BITS |
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_CHIPSELECT_ENABLE |
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_CHIPSELECT_DISABLE);

            return status;
        }

        static MPSSE.FT_STATUS ReadByte(ushort address, ref ushort data)
        {
            uint sizeToTransfer = 0;
            uint sizeTransfered;

            // CS_High + Read command.
            sizeToTransfer = 3;
            sizeTransfered = 0;
            buffer[0] = 0xc0;   // Read command (3bits).
            status = mpsse_spi.SPI_Write(buffer, sizeToTransfer, ref sizeTransfered,
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BITS |
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_CHIPSELECT_ENABLE); ;

            // Write address.
            sizeToTransfer = addressSize;
            sizeTransfered = 0;
            buffer[0] = (byte)(address << (8 - addressSize));
            status = mpsse_spi.SPI_Write(buffer, sizeToTransfer, ref sizeTransfered,
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BITS);

            // Write dummy 0 bit.
            sizeToTransfer = 1;
            sizeTransfered = 0;
            buffer[0] = 0;
            status = mpsse_spi.SPI_Write(buffer, sizeToTransfer, ref sizeTransfered,
                                         MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BITS);


            // Read 2 bytes.
            sizeToTransfer = 2;
            sizeTransfered = 0;

            status = mpsse_spi.SPI_Read(buffer, sizeToTransfer, ref sizeTransfered,
                                        MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_SIZE_IN_BYTES |
                                        MPSSE_SPI.TransferOptions.SPI_TRANSFER_OPTIONS_CHIPSELECT_DISABLE);

            data = (ushort)(buffer[1] << 8);
            data = (ushort)((data & 0xff00) | (0x00ff & buffer[0]));

            return status;
        }
    }
}

