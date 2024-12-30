# Minimum CMake version required
cmake_minimum_required(VERSION 3.26)

include_directories()

# Set policy for using VERSION in project command
cmake_policy(SET CMP0048 NEW)

# Define the project
project(libdynimp VERSION 1.0.0)

# Set build configurations
set(CMAKE_CONFIGURATION_TYPES "Release;RelWithDebInfo" CACHE STRING "" FORCE)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED True)
if(MSVC)
    add_compile_options("/utf-8")
    set(CMAKE_CXX_FLAGS_RELEASE "/Od")
    set(CMAKE_C_FLAGS_RELEASE "/Od")
endif()

file(GLOB JSON_CPP_SOURCES "vendor/jsoncpp/src/lib_json/*.cpp" "vendor/jsoncpp/include/json/*.h")

# Define the library
add_library(${PROJECT_NAME} SHARED src/Loader.cpp
        inc/Loader.hpp
        ${JSON_CPP_SOURCES})

# Include directories
target_include_directories(${PROJECT_NAME} PRIVATE "inc")
target_include_directories(${PROJECT_NAME} PRIVATE "vendor/jsoncpp/include")
target_include_directories(${PROJECT_NAME} PRIVATE "vendor/fmt/include")