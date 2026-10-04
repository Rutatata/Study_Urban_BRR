// STUB: minimal UnityEngine.Timeline (com.unity.timeline) surface needed by UniVRM. Compile-check only.
namespace UnityEngine.Timeline
{
    public interface ITimeControl
    {
        void SetTime(double time);
        void OnControlTimeStart();
        void OnControlTimeStop();
    }
}
