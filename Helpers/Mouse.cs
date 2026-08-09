using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace FollowBotV2.Helpers
{
    public static class Mouse
    {
        private const int MOUSEEVENTF_MOVE = 0x0001;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;

        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        private static extern bool SetCursorPosNative(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        public static void SetCursorPos(int x, int y)
        {
            SetCursorPosNative(x, y);
        }

        public static void SetCursorPos(MouseVector2 pos)
        {
            SetCursorPosNative((int)pos.X, (int)pos.Y);
        }

        public static MouseVector2 GetCursorPosition()
        {
            GetCursorPos(out var point);
            return new MouseVector2(point.X, point.Y);
        }

        public static void MoveCursorSmooth(MouseVector2 target, int steps = 10)
        {
            var start = GetCursorPosition();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                var pos = MouseVector2.Lerp(start, target, t);
                SetCursorPosNative((int)pos.X, (int)pos.Y);
                Thread.Sleep(1);
            }
        }

        public static void LeftClick()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            Thread.Sleep(10);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }
    }

    public struct MouseVector2
    {
        public float X, Y;

        public MouseVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static MouseVector2 Lerp(MouseVector2 a, MouseVector2 b, float t)
        {
            return new MouseVector2(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t
            );
        }

        public static implicit operator SharpDX.Vector2(MouseVector2 v)
        {
            return new SharpDX.Vector2(v.X, v.Y);
        }
    }
}