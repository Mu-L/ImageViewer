#pragma once
#include <memory>
#include "GliImage.h"

std::unique_ptr<image::IImage> ktx2_load(const char* filename);
std::vector<uint32_t> ktx2_get_export_formats();

void ktx2_save_image(const char* filename, GliImage& image, gli::format format, int quality);