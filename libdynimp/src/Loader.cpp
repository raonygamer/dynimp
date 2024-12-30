#include "Loader.hpp"

#include <fstream>
#include <iostream>
#include <json/json.h>
#define FMT_HEADER_ONLY
#include <fmt/format.h>


HMODULE LoadLibraryAndResolveDynImps(const std::string& libraryPath, const std::string& dynimpFilesPath) {
    if (!fs::exists(libraryPath) || !fs::is_regular_file(libraryPath))
        return nullptr;

    fmt::println("libdynimp: Loading module \"{}\" and resolving dynamic imports...", fs::path(libraryPath).filename().string());
    auto module = LoadLibraryA(libraryPath.c_str());

    std::vector<Json::Value> dynImps{};
    if (fs::exists(dynimpFilesPath) && fs::is_directory(dynimpFilesPath)) {
        for (const auto& entry : fs::directory_iterator(dynimpFilesPath, fs::directory_options::skip_permission_denied)) {
            if (entry.is_regular_file() && entry.path().wstring().ends_with(L".dynimp.json")) {
                try {
                    std::ifstream file(entry.path());
                    if (file.is_open()) {
                        Json::Value root;
                        Json::CharReaderBuilder reader;
                        std::string errs;
                        if (!Json::parseFromStream(reader, file, &root, &errs)) {
                            fmt::println("libdynimp: Error trying to parse dynamic import file \"{}\":\n{}", entry.path().filename().string(), errs);
                        }
                        else {
                            dynImps.push_back(root);
                        }
                    }
                }
                catch (const std::exception& ex) {
                    fmt::println("libdynimp: Error {}", ex.what());
                }
            }
        }
    }
    int resolvedImports = PatchRuntimePE(module, dynImps);
    fmt::println("libdynimp:   Resolved {} dynamic imports. :)", fs::path(libraryPath).filename().string());
    fmt::println("libdynimp: Loaded module \"{}\" successfully...", fs::path(libraryPath).filename().string());
    return module;
}

int PatchRuntimePE(HMODULE module, const std::vector<Json::Value>& dynimps) {
    int numberOfPatches = 0;
    const uint64_t moduleBase = reinterpret_cast<uint64_t>(module);
    const PIMAGE_NT_HEADERS ntHeaders = reinterpret_cast<PIMAGE_NT_HEADERS>(moduleBase + reinterpret_cast<PIMAGE_DOS_HEADER>(moduleBase)->e_lfanew);
    const uint16_t numberOfSections = ntHeaders->FileHeader.NumberOfSections;
    const PIMAGE_SECTION_HEADER sectionHeaders = IMAGE_FIRST_SECTION(ntHeaders);

    for (uint16_t sectionIndex = 0; sectionIndex < numberOfSections; sectionIndex++) {
        const std::string sectionName(reinterpret_cast<const char*>(sectionHeaders[sectionIndex].Name));
        if (sectionName == ".dynimp") {
            for (const Json::Value& dynimp : dynimps) {
                if (dynimp.empty() ||
                    dynimp.isNull() ||
                    !dynimp.isObject() ||
                    dynimp["target"].isNull() ||
                    !dynimp["target"].isString() ||
                    dynimp["imports"].isNull() ||
                    !dynimp["imports"].isArray())
                    continue;

                PIMAGE_IMPORT_DESCRIPTOR dynamicImportDescriptor = reinterpret_cast<PIMAGE_IMPORT_DESCRIPTOR>(moduleBase + sectionHeaders[sectionIndex].VirtualAddress);
                constexpr IMAGE_IMPORT_DESCRIPTOR zeroDescriptor = {};
                while (memcmp(dynamicImportDescriptor, &zeroDescriptor, sizeof(IMAGE_IMPORT_DESCRIPTOR)) != 0) {
                    const std::string importModuleName(reinterpret_cast<const char*>(moduleBase + dynamicImportDescriptor->Name));
                    if (GetModuleHandleA(importModuleName.c_str()) != nullptr && importModuleName == dynimp["target"].asString()) {
                        uint64_t targetModuleAddress = reinterpret_cast<uint64_t>(GetModuleHandleA(importModuleName.c_str()));
                        fmt::println("libdynimp: Parsing dynamic imports of {} at address 0x{:x}...", importModuleName, targetModuleAddress);
                        char moduleFileName[MAX_PATH];
                        GetModuleFileName(GetModuleHandleA(importModuleName.c_str()), moduleFileName, MAX_PATH);
                        DWORD handle;
                        const DWORD size = GetFileVersionInfoSize(moduleFileName, &handle);
                        unsigned char* fileVersionInfoData = new unsigned char[size];
                        std::string peVersion = "any";
                        if (GetFileVersionInfo(moduleFileName, handle, size, fileVersionInfoData)) {
                            VS_FIXEDFILEINFO* fileInfo;
                            uint32_t len;
                            if (VerQueryValue(fileVersionInfoData, "\\", reinterpret_cast<void**>(&fileInfo), &len)) {
                                const uint16_t major = HIWORD(fileInfo->dwFileVersionMS);
                                const uint16_t minor = LOWORD(fileInfo->dwFileVersionMS);
                                const uint16_t build = HIWORD(fileInfo->dwFileVersionLS);
                                const uint16_t revision = LOWORD(fileInfo->dwFileVersionLS);
                                peVersion = std::to_string(major) + "." + std::to_string(minor) + "." + std::to_string(build) + "." + std::to_string(revision);
                            }
                        }
                        delete[] fileVersionInfoData;
                        fmt::println("libdynimp:   Target version is {}", peVersion);

                        auto importsArray = dynimp["imports"];
                        if (!importsArray.empty() &&
                            !importsArray.isNull() &&
                            importsArray.isArray())
                        {
                            for (const auto& importEntry : importsArray) {
                                bool processedEntry = false;
                                uint64_t* importLookupTable = reinterpret_cast<uint64_t*>(moduleBase + dynamicImportDescriptor->OriginalFirstThunk);
                                uint64_t* importAddressTable = reinterpret_cast<uint64_t*>(moduleBase + dynamicImportDescriptor->FirstThunk);

                                if (importEntry.empty() || importEntry.isNull() || !importEntry.isObject() || importEntry["symbol"].isNull() || !importEntry["symbol"].isString() ||
                                    importEntry["points"].isNull() || !importEntry["points"].isArray())
                                    continue;

                                while (*importLookupTable != 0 && *importAddressTable != 0) {
                                    if (processedEntry)
                                        break;

                                    if ((*importLookupTable & 1ull << 63) == 0) {
                                        const PIMAGE_IMPORT_BY_NAME hintNameTable = reinterpret_cast<PIMAGE_IMPORT_BY_NAME>(moduleBase + *importLookupTable);
                                        const std::string importFunctionName(hintNameTable->Name);
                                        if (importEntry["symbol"].asString() == importFunctionName) {
                                            fmt::println("libdynimp:   Processing {}", importFunctionName);
                                            for (const auto& importPoint : importEntry["points"]) {
                                                if (processedEntry)
                                                    break;

                                                if (importPoint.empty() ||
                                                    importPoint.isNull() ||
                                                    !importPoint.isObject() ||
                                                    importPoint["value"].isNull() ||
                                                    !importPoint["value"].isString() ||
                                                    importPoint["version"].isNull() ||
                                                    !importPoint["version"].isString())
                                                    continue;

                                                const std::string targetPointVersion = importPoint["version"].asString();
                                                const std::string pointType = !importPoint["type"].isNull() && importPoint["type"].isString() ?
                                                    importPoint["type"].asString() :
                                                    "address";
                                                const std::string pointValue = importPoint["value"].asString();

                                                if (targetPointVersion == "any" || targetPointVersion == peVersion) {
                                                    if (pointType == "address") {
                                                        try {
                                                            uint64_t address = targetModuleAddress + std::stoul(pointValue, nullptr, 16);
                                                            DWORD oldProt;
                                                            VirtualProtectEx(GetCurrentProcess(), importAddressTable, sizeof(uint64_t), PAGE_EXECUTE_READWRITE, &oldProt);
                                                            *importAddressTable = address;
                                                            fmt::println("libdynimp:     Pointing that to 0x{:x} (0x{:x} + 0x{:x})", address, targetModuleAddress, address - targetModuleAddress);
                                                            VirtualProtectEx(GetCurrentProcess(), importAddressTable, sizeof(uint64_t), oldProt, nullptr);
                                                        }
                                                        catch (...) {
                                                            continue;
                                                        }
                                                    }
                                                    else if (pointType == "signature") {
                                                        continue;
                                                    }
                                                    else {
                                                        continue;
                                                    }
                                                    processedEntry = true;
                                                    numberOfPatches++;
                                                }
                                            }
                                        }
                                    }

                                    importLookupTable++;
                                    importAddressTable++;
                                }
                            }
                        }
                    }
                    dynamicImportDescriptor++;
                }
            }
            break;
        }
    }
    return numberOfPatches;
}