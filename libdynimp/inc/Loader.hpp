#pragma once
#include <string>
#include <windows.h>
#include <filesystem>
#pragma comment(lib, "Version.lib")

namespace fs = std::filesystem;

namespace Json {
    class Value;
}

extern "C" __declspec(dllexport) HMODULE __stdcall LoadLibraryAndResolveDynImps(const std::string& libraryPath, const std::string& dynimpFilesPath);
int PatchRuntimePE(HMODULE module, const std::vector<Json::Value>& dynimps);