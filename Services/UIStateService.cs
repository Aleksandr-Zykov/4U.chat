using System;

namespace _4U.chat.Services
{
    public class UIStateService
    {
        public bool IsSidebarCollapsed { get; private set; }
        public bool IsDarkTheme { get; private set; } = true;

        public event Action? OnChange;

        public void ToggleSidebar()
        {
            IsSidebarCollapsed = !IsSidebarCollapsed;
            NotifyStateChanged();
        }

        public void SetTheme(bool isDark)
        {
            if (IsDarkTheme != isDark)
            {
                IsDarkTheme = isDark;
                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
