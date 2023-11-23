﻿using Hoscy.Ui.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Hoscy.Ui
{
    internal static class UiHelper
    {
        #region Colors
        //Back
        public static readonly SolidColorBrush ColorBackDark = new(Color.FromRgb(37, 37, 37));
        public static readonly SolidColorBrush ColorBack = new(Color.FromRgb(52, 52, 52));
        public static readonly SolidColorBrush ColorBackLight = new(Color.FromRgb(77, 77, 77));

        //Front
        public static readonly SolidColorBrush ColorFront = new(Color.FromRgb(255, 255, 255));
        public static readonly SolidColorBrush ColorFrontDark = new(Color.FromRgb(200, 200, 200));

        //Extra
        public static readonly SolidColorBrush ColorValid = new(Color.FromRgb(202, 255, 191));
        public static readonly SolidColorBrush ColorInvalid = new(Color.FromRgb(255, 173, 173));
        #endregion

        #region Expansion Methods
        /// <summary>
        /// Loads data into a combo box
        /// </summary>
        internal static void Load<T>(this ComboBox box, IEnumerable<T> source, int index)
        {
            box.ItemsSource = source.ToArray();
            box.SelectedIndex = index;
        }

        /// <summary>
        /// Loads dictionary data into a combo box
        /// </summary>
        internal static void LoadDictionary(this ComboBox box, Dictionary<string, string> dict, string indexKey)
        {
            int index = -1;
            var keyArray = dict.Keys.ToArray();
            for (int i = 0; i < keyArray.Length; i++)
            {
                if (indexKey == keyArray[i])
                {
                    index = i;
                    break;
                }
            }
            box.Load(keyArray, index);
        }

        /// <summary>
        /// Loads data into a ListBox
        /// </summary>
        internal static void Load<T>(this ListBox box, IEnumerable<T> source, int index)
        {
            box.ItemsSource = source.ToArray();
            box.SelectedIndex = index;
        }

        /// <summary>
        /// Checks if a value is in bounds
        /// </summary>
        internal static bool IsInBounds<T>(this ListBox box, ICollection<T> list)
        {
            int highVal = list.Count - 1;
            if (box.SelectedIndex > highVal)
                box.SelectedIndex = highVal;

            return box.SelectedIndex > -1;
        }

        /// <summary>
        /// Tries to remove object at index
        /// </summary>
        internal static bool TryRemoveAt<T>(this List<T> list, int index)
        {
            if (index < 0 || index >= list.Count)
                return false;

            list.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Tries to modify object at index
        /// </summary>
        internal static bool TryModifyAt<T>(this List<T> list, T item, int index)
        {
            if (index < 0 || index >= list.Count)
                return false;

            list[index] = item;
            return true;
        }

        /// <summary>
        /// Sets window to dark mode and calls ShowDialog
        /// </summary>
        internal static void ShowDialogDark(this Window window)
        {
            window.SetDarkMode(true);
            window.ShowDialog();
        }
        #endregion

        #region Opening Modify Windows
        internal static void OpenListEditor(string title, string valueName, List<string> list, string defaultString = "New Value")
        {
            var window = new ModifyListWindow(title, valueName, list, defaultString);
            window.ShowDialogDark();
        }

        internal static void OpenDictionaryEditor(string title, string keyName, string valueName, Dictionary<string, string> dict)
        {
            var window = new ModifyDictionaryWindow(title, keyName, valueName, dict);
            window.ShowDialogDark();
        }
        #endregion

        #region Window Darkmode - Thanks Hekky
        [DllImport("Dwmapi.dll", SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, [In] ref uint pvAttribute, uint cbAttribute);

        private enum DWMWINDOWATTRIBUTE : uint
        {
            DWMWA_NCRENDERING_ENABLED = 1,
            DWMWA_NCRENDERING_POLICY,
            DWMWA_TRANSITIONS_FORCEDISABLED,
            DWMWA_ALLOW_NCPAINT,
            DWMWA_CAPTION_BUTTON_BOUNDS,
            DWMWA_NONCLIENT_RTL_LAYOUT,
            DWMWA_FORCE_ICONIC_REPRESENTATION,
            DWMWA_FLIP3D_POLICY,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            DWMWA_HAS_ICONIC_BITMAP,
            DWMWA_DISALLOW_PEEK,
            DWMWA_EXCLUDED_FROM_PEEK,
            DWMWA_CLOAK,
            DWMWA_CLOAKED,
            DWMWA_FREEZE_REPRESENTATION,
            DWMWA_PASSIVE_UPDATE_MODE,
            DWMWA_USE_HOSTBACKDROPBRUSH,
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            DWMWA_WINDOW_CORNER_PREFERENCE = 33,
            DWMWA_BORDER_COLOR,
            DWMWA_CAPTION_COLOR,
            DWMWA_TEXT_COLOR,
            DWMWA_VISIBLE_FRAME_BORDER_THICKNESS,
            DWMWA_SYSTEMBACKDROP_TYPE = 38,
            DWMWA_LAST,
            DWMWA_MICA_EFFECT = 1029,
        }

        internal static bool SetDarkMode(this Window window, bool darkMode)
        {
            try
            {
                var windowHandle = new WindowInteropHelper(window).EnsureHandle();

                uint value = darkMode ? 1U : 0U;
                return DwmSetWindowAttribute(windowHandle, (uint)DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, (uint)Marshal.SizeOf(value)) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion
    }
}
