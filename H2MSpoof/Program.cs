using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

const string PROCESS_NAME = "h2m-mod";
string? discordId = null;

while (string.IsNullOrWhiteSpace(discordId))
{
    Console.Write("Please enter your Discord ID: ");
    discordId = Console.ReadLine();
}

byte[] currentDiscordId = Encoding.UTF8.GetBytes(discordId.Trim());

string newDiscordId = ""; // ID to spoof

if (newDiscordId.Length > 18 || newDiscordId.Length < 16)
{
    Console.WriteLine("Please enter a valid Discord ID.");
    return;
}

byte[] newDiscordIdBytes = Encoding.UTF8.GetBytes(newDiscordId);


Process[] processes = Process.GetProcessesByName(PROCESS_NAME);
Console.WriteLine("Waiting for process \"{0}\"", PROCESS_NAME);

while (processes.Length <= 0)
{
    processes = Process.GetProcessesByName(PROCESS_NAME);
    await Task.Delay(10);
}

Process process = processes[0];

Console.WriteLine("Waiting for window...\n");

IntPtr mainWindow = IntPtr.Zero;
while (mainWindow == IntPtr.Zero)
{
    mainWindow = Memory.FindWindow(null, "H2M-Mod");

    if (mainWindow == IntPtr.Zero)
    {
        await Task.Delay(1000);
    }
}

IntPtr processHandle = Memory.OpenProcess(Memory.PROCESS_VM_READ | Memory.PROCESS_VM_WRITE | Memory.PROCESS_VM_OPERATION, false, process.Id);

if (processHandle == IntPtr.Zero)
{
    Console.WriteLine("Failed to open process.");
    return;
}

IntPtr address = IntPtr.Zero;
bool finalAddress = false;

while (!finalAddress)
{
    if (!Memory.VirtualQueryEx(processHandle, address, out Memory.MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf(typeof(Memory.MEMORY_BASIC_INFORMATION))))
        break;

    if ((mbi.Protect & Memory.PAGE_READWRITE) != 0 && mbi.State == 0x1000)
    {
        IntPtr currentAddress = mbi.BaseAddress;

        long regionSize = mbi.RegionSize;

        long bytesToRead = regionSize;

        while (bytesToRead > 0)
        {
            int chunkSize = (int)Math.Min(bytesToRead, 1024 * 1024);
            byte[] buffer = new byte[chunkSize];

            if (Memory.ReadProcessMemory(processHandle, currentAddress, buffer, buffer.Length, out int bytesRead))
            {
                for (int i = 0; i < bytesRead - currentDiscordId.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < currentDiscordId.Length; j++)
                    {
                        if (buffer[i + j] != currentDiscordId[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        string hex = $"0x{(currentAddress + i):X}";

                        WriteLineColour($"Found string at address: {hex}", ConsoleColor.Magenta);

                        for (int j = 0; j < newDiscordIdBytes.Length; j++)
                        {
                            buffer[i + j] = newDiscordIdBytes[j];
                        }

                        for (int j = newDiscordIdBytes.Length; j < currentDiscordId.Length; j++)
                        {
                            buffer[i + j] = 0x00;
                        }

                        WriteLineColour("Replaced bytes.", ConsoleColor.Yellow);

                        Memory.WriteProcessMemory(processHandle, currentAddress + i, buffer.Skip(i).Take(newDiscordIdBytes.Length).ToArray(), newDiscordIdBytes.Length, out int bytesWritten);
                        WriteLineColour("Bytes written.\n", ConsoleColor.Green);

                        if (hex.StartsWith("0xF")) // save time skipping after this if using original exe otherwise will check and change unnecessary static address
                        {
                            await Finish();
                        }
                    }
                }
            }

            currentAddress = IntPtr.Add(currentAddress, chunkSize);
            bytesToRead -= chunkSize;
        }
    }

    address = new IntPtr(mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64());
}

await Finish();

async Task Finish()
{
    WriteLineColour("Finished.", ConsoleColor.Green);

    Memory.CloseHandle(processHandle);

    await Task.Delay(1500);

    Environment.Exit(0);
}

static void WriteLineColour(string text, ConsoleColor color)
{
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ForegroundColor = ConsoleColor.White;
}

public class Memory
{
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll")]
    public static extern bool VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    public const int PROCESS_VM_READ = 0x0010;
    public const int PROCESS_VM_WRITE = 0x0020;
    public const int PROCESS_VM_OPERATION = 0x0008;
    public const int PAGE_READWRITE = 0x04;
}
