#include <Windows.h>

#include <detours.h>

#include <algorithm>
#include <string>

extern "C"
__declspec(dllexport)
BOOL
StartProcessPreloadedW(
    LPCWSTR lpApplicationName,
    LPCWSTR lpDllName
)
{
    STARTUPINFOW si = {};
    si.cb = sizeof(si);
    PROCESS_INFORMATION pi;

    std::wstring dllNameW = lpDllName;
    std::string dllName;
    dllName.reserve(dllNameW.size());
    std::transform(
        dllNameW.begin(), dllNameW.end(),
        std::back_inserter(dllName),
        [](auto ch) { return (char)ch; }
    );

    BOOL result = DetourCreateProcessWithDllW(
        lpApplicationName,
        NULL,
        NULL,
        NULL,
        FALSE,
        0,
        NULL,
        NULL,
        &si,
        &pi,
        dllName.c_str(),
        NULL
    );

    if (result)
    {
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
    }

    return result;
}
