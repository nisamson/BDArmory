using UnityEngine;
using Random = UnityEngine.Random;

using BDArmory.Core;
using BDArmory.Misc;

namespace BDArmory.UI
{
    public static class BDGUIUtils
    {
        public static Texture2D pixel;

        public static Camera GetMainCamera()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                return FlightCamera.fetch.mainCamera;
            }
            else
            {
                return Camera.main;
            }
        }

        public static void DrawTextureOnWorldPos(Vector3 worldPos, Texture texture, Vector2 size, float wobble)
        {
            Vector3 screenPos = GetMainCamera().WorldToViewportPoint(worldPos);
            if (screenPos.z < 0) return; //dont draw if point is behind camera
            if (screenPos.x != Mathf.Clamp01(screenPos.x)) return; //dont draw if off screen
            if (screenPos.y != Mathf.Clamp01(screenPos.y)) return;
            float xPos = screenPos.x * Screen.width - (0.5f * size.x);
            float yPos = (1 - screenPos.y) * Screen.height - (0.5f * size.y);
            if (wobble > 0)
            {
                xPos += Random.Range(-wobble / 2, wobble / 2);
                yPos += Random.Range(-wobble / 2, wobble / 2);
            }
            Rect iconRect = new Rect(xPos, yPos, size.x, size.y);

            GUI.DrawTexture(iconRect, texture);
        }

        public static bool WorldToGUIPos(Vector3 worldPos, out Vector2 guiPos)
        {
            Vector3 screenPos = GetMainCamera().WorldToViewportPoint(worldPos);
            bool offScreen = false;
            if (screenPos.z < 0) offScreen = true; //dont draw if point is behind camera
            if (screenPos.x != Mathf.Clamp01(screenPos.x)) offScreen = true; //dont draw if off screen
            if (screenPos.y != Mathf.Clamp01(screenPos.y)) offScreen = true;
            if (!offScreen)
            {
                float xPos = screenPos.x * Screen.width;
                float yPos = (1 - screenPos.y) * Screen.height;
                guiPos = new Vector2(xPos, yPos);
                return true;
            }
            else
            {
                guiPos = Vector2.zero;
                return false;
            }
        }

        public static void DrawLineBetweenWorldPositions(Vector3 worldPosA, Vector3 worldPosB, float width, Color color)
        {
            Camera cam = GetMainCamera();

            if (cam == null) return;

            GUI.matrix = Matrix4x4.identity;

            bool aBehind = false;

            Plane clipPlane = new Plane(cam.transform.forward, cam.transform.position + cam.transform.forward * 0.05f);

            if (Vector3.Dot(cam.transform.forward, worldPosA - cam.transform.position) < 0)
            {
                Ray ray = new Ray(worldPosB, worldPosA - worldPosB);
                float dist;
                if (clipPlane.Raycast(ray, out dist))
                {
                    worldPosA = ray.GetPoint(dist);
                }
                aBehind = true;
            }
            if (Vector3.Dot(cam.transform.forward, worldPosB - cam.transform.position) < 0)
            {
                if (aBehind) return;

                Ray ray = new Ray(worldPosA, worldPosB - worldPosA);
                float dist;
                if (clipPlane.Raycast(ray, out dist))
                {
                    worldPosB = ray.GetPoint(dist);
                }
            }

            Vector3 screenPosA = cam.WorldToViewportPoint(worldPosA);
            screenPosA.x = screenPosA.x * Screen.width;
            screenPosA.y = (1 - screenPosA.y) * Screen.height;
            Vector3 screenPosB = cam.WorldToViewportPoint(worldPosB);
            screenPosB.x = screenPosB.x * Screen.width;
            screenPosB.y = (1 - screenPosB.y) * Screen.height;

            screenPosA.z = screenPosB.z = 0;

            float angle = Vector2.Angle(Vector3.up, screenPosB - screenPosA);
            if (screenPosB.x < screenPosA.x)
            {
                angle = -angle;
            }

            Vector2 vector = screenPosB - screenPosA;
            float length = vector.magnitude;

            Rect upRect = new Rect(screenPosA.x - (width / 2), screenPosA.y - length, width, length);

            GUIUtility.RotateAroundPivot(-angle + 180, screenPosA);
            DrawRectangle(upRect, color);
            GUI.matrix = Matrix4x4.identity;
        }

        public static void DrawRectangle(Rect rect, Color color)
        {
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1);
            }

            Color originalColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = originalColor;
        }

        public static void MarkPosition(Transform transform, Color color) => MarkPosition(transform.position, transform, color);

        public static void MarkPosition(Vector3 position, Transform transform, Color color, float size = 3, float thickness = 2)
        {
            DrawLineBetweenWorldPositions(position + transform.right * size, position - transform.right * size, thickness, color);
            DrawLineBetweenWorldPositions(position + transform.up * size, position - transform.up * size, thickness, color);
            DrawLineBetweenWorldPositions(position + transform.forward * size, position - transform.forward * size, thickness, color);
        }

        public static void UseMouseEventInRect(Rect rect)
        {
            if (Event.current == null) return;
            if (Utils.MouseIsInRect(rect) && Event.current.isMouse && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp))
            {
                Event.current.Use();
            }
        }

        public static Rect CleanRectVals(Rect rect)
        {
            // Remove decimal places so Mac does not complain.
            rect.x = (int)rect.x;
            rect.y = (int)rect.y;
            rect.width = (int)rect.width;
            rect.height = (int)rect.height;
            return rect;
        }

        internal static void RepositionWindow(ref Rect windowPosition)
        {
            if (BDArmorySettings.STRICT_WINDOW_BOUNDARIES)
            {
                // This method uses Gui point system.
                if (windowPosition.x < 0) windowPosition.x = 0;
                if (windowPosition.y < 0) windowPosition.y = 0;

                if (windowPosition.xMax > Screen.width) // Don't go off the right of the screen.
                    windowPosition.x = Screen.width - windowPosition.width;
                if (windowPosition.height > Screen.height) // Don't go off the top of the screen.
                    windowPosition.y = 0;
                else if (windowPosition.yMax > Screen.height) // Don't go off the bottom of the screen.
                    windowPosition.y = Screen.height - windowPosition.height;
            }
            else // If the window is completely off-screen, bring it just onto the screen.
            {
                if (windowPosition.width == 0) windowPosition.width = 1;
                if (windowPosition.height == 0) windowPosition.height = 1;
                if (windowPosition.x >= Screen.width) windowPosition.x = Screen.width - 1;
                if (windowPosition.y >= Screen.height) windowPosition.y = Screen.height - 1;
                if (windowPosition.x + windowPosition.width < 1) windowPosition.x = 1 - windowPosition.width;
                if (windowPosition.y + windowPosition.height < 1) windowPosition.y = 1 - windowPosition.height;
            }
        }

        internal static Rect GuiToScreenRect(Rect rect)
        {
            // Must run during OnGui to work...
            Rect newRect = new Rect
            {
                position = GUIUtility.GUIToScreenPoint(rect.position),
                width = rect.width,
                height = rect.height
            };
            return newRect;
        }
    }
}
