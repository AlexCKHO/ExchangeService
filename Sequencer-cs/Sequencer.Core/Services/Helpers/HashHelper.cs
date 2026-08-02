using System;
using System.IO.Hashing;
using Sequencer.Core.Domain;

namespace Sequencer.Core.Services;

public static unsafe class HashHelper
{
    /// <summary>
    /// Calculates the checksum for the entire Record (Header + Payload), precisely excluding the FrameLength field and the CheckSum itself.
    /// </summary>
    /// <param name="recordPtr">The absolute starting point of the entire Record in memory (pointing to the Header's FrameLength).</param>
    /// <param name="totalLength">The total length of the entire Record (32 + sizeof(T)).</param>
    public static unsafe uint CalculateChecksum(byte* recordPtr, int totalLength)
    {
        XxHash32 hash = new XxHash32();

        // Treat the starting pointer as a RecordHeader
        RecordHeader* header = (RecordHeader*)recordPtr;

        // ==========================================
        // Range 1: From Command up to before CheckSum
        // ==========================================
        // Get the actual memory address of Command (&)
        byte* part1Start = (byte*)&header->Command;

        // Get the address of CheckSum, subtract the address of Command, and the compiler will automatically calculate the length (20)!
        int part1Length = (int)((byte*)&header->CheckSum - part1Start);

        hash.Append(new ReadOnlySpan<byte>(part1Start, part1Length));

        // ==========================================
        // Range 2: From Reserved to the end of the Payload
        // ==========================================
        if (totalLength > sizeof(RecordHeader))
        {
            // Get the actual memory address of Reserved
            byte* part2Start = (byte*)&header->Reserved;

            // Subtract the offset of Reserved (distance from the start) from the total length
            int part2Length = totalLength - (int)(part2Start - recordPtr);

            hash.Append(new ReadOnlySpan<byte>(part2Start, part2Length));
        }

        return hash.GetCurrentHashAsUInt32();
    }
}