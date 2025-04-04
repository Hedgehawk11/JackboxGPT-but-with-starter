#nullable enable
namespace JackboxGPT.Games.Common.Models
{
    public readonly struct Revision<T>
    {
        public Revision(T old, T @new)
        {
            Old = old;
            New = @new;
        }
        
        public T Old { get; }
        public T New { get; }
    }
}