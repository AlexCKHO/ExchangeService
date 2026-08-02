namespace Sequencer.Core.Domain.Enums;

public enum CommandType : byte
{
    UNSPECIFIED = 0,
    ORDERREQUEST = 1,
    CANCELREQUEST = 2
}