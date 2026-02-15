#include "pch.h"
#include "webp_interface.h"
#include <webp/decode.h>
#include <webp/encode.h>
#include <webp/demux.h>
#include <fstream>
#include <vector>
#include <stdexcept>

std::unique_ptr<image::IImage> webp_load(const char* filename)
{
    std::ifstream file(filename, std::ios::binary | std::ios::ate);
    if (!file)
        throw std::runtime_error("could not open file");

    std::streamsize size = file.tellg();
    file.seekg(0, std::ios::beg);
    std::vector<uint8_t> buffer(size);
    if (!file.read(reinterpret_cast<char*>(buffer.data()), size))
        throw std::runtime_error("could not read file");

	file.close(); // finished with file

    // get rgba data
    int width = 0, height = 0;
    uint8_t* rgba = WebPDecodeRGBA(buffer.data(), buffer.size(), &width, &height);
    if (!rgba)
        throw std::runtime_error("WebP decode failed");

    // copy to simple image
    auto img = std::make_unique<image::SimpleImage>(
        gli::format::FORMAT_RGBA8_SRGB_PACK8,
        gli::format::FORMAT_RGBA8_SRGB_PACK8,
        width, height, 4);
    size_t imgDataSize;
    uint8_t* imgData = img->getData(0, 0, imgDataSize);
    std::memcpy(imgData, rgba, width * height * 4);

    
    WebPFree(rgba);

    return img;
}