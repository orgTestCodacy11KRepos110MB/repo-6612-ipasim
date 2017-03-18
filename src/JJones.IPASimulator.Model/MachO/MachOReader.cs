﻿using JJones.IPASimulator.Model.Conversion;
using JJones.IPASimulator.Model.IO;
using MiscUtil.Conversion;
using MiscUtil.IO;
using System;
using System.IO;

namespace JJones.IPASimulator.Model.MachO
{
    public class MachOReader : IDisposable
    {
        private readonly GeneralEndianBitConverter bitConverter;
        private readonly EndianBinaryReader rdr, peekingRdr;

        public MachOReader(Stream stream)
        {
            var peekableStream = new PeekableStream(new CountingStream(stream), 4);
            bitConverter = new GeneralEndianBitConverter(Endianness.BigEndian);
            rdr = new EndianBinaryReader(bitConverter, peekableStream);
            peekingRdr = new EndianBinaryReader(bitConverter, new PeekingStream(peekableStream));
        }

        public uint NFatArch { get; private set; }
        public MachHeader MachHeader { get; private set; }

        public bool TryReadHeader()
        {
            return TryReadFatHeader() ||
                TryReadMachHeader();
        }
        public bool TryReadFatHeader()
        {
            if (IsMagic(0xCAFEBABE))
            {
                NFatArch = rdr.ReadUInt32();
                return true;
            }
            return false;
        }
        public bool TryReadMachHeader()
        {
            var magic = peekingRdr.ReadUInt32();
            MachHeaderKind kind;
            if (magic == 0xFEEDFACE)
            {
                kind = MachHeaderKind.x86;
            }
            else if (magic == 0xCEFAEDFE)
            {
                kind = MachHeaderKind.x86;
                bitConverter.SwitchEndianness();
            }
            else if (magic == 0xFEEDFACF)
            {
                kind = MachHeaderKind.x64;
            }
            else if (magic == 0xCFFAEDFE)
            {
                kind = MachHeaderKind.x64;
                bitConverter.SwitchEndianness();
            }
            else
            {
                return false;
            }
            rdr.ReadUInt32(); // magic
            
            MachHeader = new MachHeader
            (
                kind,
                (CpuType)rdr.ReadUInt32(),
                rdr.ReadUInt32(),
                (FileType)rdr.ReadUInt32(),
                rdr.ReadUInt32(),
                rdr.ReadUInt32(),
                (Flags)rdr.ReadUInt32()
            );
            if (kind == MachHeaderKind.x64)
            {
                rdr.ReadUInt32(); // reserved
            }
            return true;
        }
        public FatArchitecture ReadFatArch()
        {
            return new FatArchitecture
            (
                (CpuType)rdr.ReadUInt32(),
                rdr.ReadUInt32(),
                rdr.ReadUInt32(),
                rdr.ReadUInt32(),
                rdr.ReadUInt32()
            );
        }
        public void SeekArch(FatArchitecture arch)
        {
            var count = arch.Offset - rdr.BaseStream.Position;
            var buffer = new byte[1024];
            var read = 0L;
            while (read < count)
            {
                var toRead = (int)Math.Min(count - read, 1024);
                var result = rdr.Read(buffer, 0, toRead);
                if (result != toRead)
                {
                    throw new EndOfStreamException();
                }
                read += result;
            }
        }
        public LoadCommand ReadLoadCommand()
        {
            return new LoadCommand
            (
                (LoadCommandType)rdr.ReadUInt32(),
                rdr.ReadUInt32()
            );
        }
        public SegmentCommand ReadSegmentCommand(LoadCommand header)
        {
            return new SegmentCommand
            (
                header.Size,
                rdr.ReadNullPaddedString(16),
                rdr.ReadUInt32(),
                rdr.ReadUInt32(),
                rdr.ReadUInt32(),
                rdr.ReadUInt32(),
                (VmProtection)rdr.ReadInt32(),
                (VmProtection)rdr.ReadInt32(),
                rdr.ReadUInt32(),
                rdr.ReadUInt32()
            );
        }
        public void Dispose()
        {
            rdr.Dispose();
            peekingRdr.Dispose();
        }

        private bool IsMagic(uint value)
        {
            var magic = peekingRdr.ReadUInt32();
            if (magic == value)
            {
                rdr.ReadUInt32(); // Skips magic.
                return true;
            }
            return false;
        }
    }
}
