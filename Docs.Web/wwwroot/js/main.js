// Toggles which Prism theme stylesheet is active. Called from Blazor whenever
// the dark mode state changes. The dark stylesheet has id="prism-dark" in
// index.html and starts disabled — this function flips its `disabled` flag.
window.setPrismDarkTheme = (isDark) => {
    const darkSheet = document.getElementById("prism-dark");
    if (darkSheet) {
        darkSheet.disabled = !isDark;
    }
};
