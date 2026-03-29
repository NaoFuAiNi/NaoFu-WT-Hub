/* 根目录启动器：启动 bin\NaoFu WT Hub 2.1.2.exe，工作目录为根目录 */

#define WIN32_LEAN_AND_MEAN
#define _CRT_SECURE_NO_WARNINGS
#include <windows.h>
#include <stdio.h>

#define BIN_EXE L"bin\\NaoFu WT Hub 2.1.2.exe"
#define MAX_PATH_W 1024

int WINAPI wWinMain(HINSTANCE hInst, HINSTANCE hPrev, LPWSTR lpCmdLine, int nShow)
{
    (void)hInst;
    (void)hPrev;
    (void)lpCmdLine;
    (void)nShow;
    WCHAR self[MAX_PATH_W], dir[MAX_PATH_W], target[MAX_PATH_W];
    WCHAR *p;
    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi = { 0 };

    if (GetModuleFileNameW(NULL, self, MAX_PATH_W) == 0)
        return 1;
    wcscpy_s(dir, MAX_PATH_W, self);
    p = wcsrchr(dir, L'\\');
    if (p) *p = L'\0';
    else  return 1;
    if (dir[0] == L'\0') return 1;
    swprintf_s(target, MAX_PATH_W, L"%s\\" BIN_EXE, dir);

    if (GetFileAttributesW(target) == INVALID_FILE_ATTRIBUTES)
        return 1;

    if (!CreateProcessW(target, NULL, NULL, NULL, FALSE, 0, NULL, dir, &si, &pi))
        return 1;
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return 0;
}
