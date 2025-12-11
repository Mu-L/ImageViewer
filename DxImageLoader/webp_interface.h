#pragma once
#include "Image.h"
#include <memory>

std::unique_ptr<image::IImage> webp_load(const char* filename);