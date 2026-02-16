#pragma once
#include "Image.h"
#include <memory>

std::unique_ptr<image::IImage> webp_load(const char* filename);

std::vector<uint32_t> webp_get_export_formats();

void webp_save_image(const char* filename, image::IImage& image, gli::format format, int quality, float fps);