using UnityEngine;

// Holds gaze selection until the dot has moved, right after the options appear.
//
// OptionReveal shows the options once the user is seen looking at the symbol, so at that moment
// they are looking at the symbol; a real answer then needs the gaze to jump to an option. But the
// option areas reach to within ~90 px of the symbol, and with single-eye error of 80-120 px the dot
// can already sit inside one while the user is still reading - in the worst single-eye run 38% of
// answers completed within 1.7 s of the options appearing (the dwell alone is 1.5 s). So after
// such a reveal no option may start filling until the dot has moved clearly away from where it was.
public static class GazeSelectionGate
{
    // Well beyond the ~11 px scatter within a fixation and the 51 px fixation radius of the dot.
    private const float MoveToOpenPx = 60f;

    private static bool s_closed;
    private static bool s_blockLogged;
    private static Vector2 s_from;
    private static FollowGazePoint2D s_pointer;

    // Close the gate at the dot's current position.
    public static void Close(FollowGazePoint2D pointer)
    {
        s_pointer = pointer;
        s_from = pointer.DisplayedScreenPosition;
        s_closed = true;
        s_blockLogged = false;
    }

    public static void Open()
    {
        s_closed = false;
    }

    // Asked by ButtonTrigger whenever the dot is on a button and a dwell would build up.
    public static bool Allows()
    {
        if (!s_closed)
            return true;
        if (s_pointer == null || !s_pointer.isActiveAndEnabled)
        {
            s_closed = false;
            return true;
        }
        if (Vector2.Distance(s_pointer.DisplayedScreenPosition, s_from) > MoveToOpenPx)
        {
            s_closed = false;
            return true;
        }
        if (!s_blockLogged)
        {
            s_blockLogged = true;
            Debug.Log("[GATE] held a selection: the dot was on an option without having moved since the options appeared");
        }
        return false;
    }
}
