#include "pch.h"
#include "webp_interface.h"
#define SIMPLEWEBP_IMPLEMENTATION
#include "../dependencies/simplewebp.h"
#include <stdexcept>

std::unique_ptr<image::IImage> webp_load(const char* filename)
{
	size_t width, height;
	simplewebp* swebp;

	auto err = simplewebp_load_from_filename(filename, NULL, &swebp);
	if (err != SIMPLEWEBP_NO_ERROR)
	{
		throw std::runtime_error("cannot open webp image");
	}
	
	simplewebp_get_dimensions(swebp, &width, &height);
	
	auto res = std::make_unique<image::SimpleImage>(
		gli::format::FORMAT_RGBA8_SRGB_PACK8,
		gli::format::FORMAT_RGBA8_SRGB_PACK8,
		(uint32_t)width, (uint32_t)height, 4
	);
	size_t imgSize;
	auto dst = res->getData(0, 0, imgSize);
	simplewebp_decode(swebp, dst, NULL);
	simplewebp_unload(swebp);

	return res;
}