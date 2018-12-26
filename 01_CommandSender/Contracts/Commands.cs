using System;

namespace Contracts
{
    public interface FirstCommand 
    {
        int Version { get; set; }
    }

    public interface SecondCommand
    {
        string Name { get; set; }
    }
}
