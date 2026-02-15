#include "pch.h"
#include "webp_interface.h"
#include "interface.h"
#include <webp/decode.h>
#include <webp/encode.h>
#include <webp/demux.h>
#include <fstream>
#include <vector>
#include <stdexcept>

class WebpImage : public image::IImage
{
public:
    WebpImage(const char* filename)
    {
        // Read file into buffer
        std::ifstream file(filename, std::ios::binary | std::ios::ate);
        if (!file)
            throw std::runtime_error("could not open file");
        std::streamsize size = file.tellg();
        file.seekg(0, std::ios::beg);
        m_buffer.resize(size);
        if (!file.read(reinterpret_cast<char*>(m_buffer.data()), size))
            throw std::runtime_error("could not read file");
        file.close();

        // Get original format
        WebPBitstreamFeatures features;
        if (WebPGetFeatures(m_buffer.data(), m_buffer.size(), &features) != VP8_STATUS_OK)
            throw std::runtime_error("WebPGetFeatures failed");

        m_hasAlpha = features.has_alpha != 0;
        m_originalFormat = m_hasAlpha ? gli::format::FORMAT_RGBA8_SRGB_PACK8 : gli::format::FORMAT_RGB8_SRGB_PACK8;

        // Demux for animation
        WebPData webp_data;
        webp_data.bytes = m_buffer.data();
        webp_data.size = m_buffer.size();
        WebPDemuxer* demux = WebPDemux(&webp_data);
        if (!demux)
            throw std::runtime_error("WebPDemux failed");

        m_frameCount = WebPDemuxGetI(demux, WEBP_FF_FRAME_COUNT);
        m_width = WebPDemuxGetI(demux, WEBP_FF_CANVAS_WIDTH);
        m_height = WebPDemuxGetI(demux, WEBP_FF_CANVAS_HEIGHT);

        m_frames.reserve(m_frameCount);
        std::vector<uint8_t> prevFrame(m_width * m_height * 4, 0);

        WebPIterator iter;
        if (!WebPDemuxGetFrame(demux, 1, &iter))
            throw std::runtime_error("WebPDemuxGetFrame failed");

        do {
            int decodeWidth = 0, decodeHeight = 0;
            uint8_t* decoded = WebPDecodeRGBA(iter.fragment.bytes, iter.fragment.size, &decodeWidth, &decodeHeight);
            if (!decoded)
                throw std::runtime_error("WebP frame decode failed");

            // Start with previous frame (or clear for first frame)
            std::vector<uint8_t> frame(prevFrame);

            // Composite the decoded region into the frame at the correct offset
			auto frameIndex = m_frames.size();
            for (int y = 0; y < decodeHeight; ++y) {
                set_progress((uint32_t)((frameIndex * m_height + y) * 100) / (m_frameCount * m_height));
                
                int destY = iter.y_offset + y;
                if (destY < 0 || destY >= int(m_height)) continue;
                for (int x = 0; x < decodeWidth; ++x) {
                    int destX = iter.x_offset + x;
                    if (destX < 0 || destX >= int(m_width)) continue;
                    size_t dstIdx = (destY * m_width + destX) * 4;
                    size_t srcIdx = (y * decodeWidth + x) * 4;

                    if (iter.blend_method == WEBP_MUX_BLEND && decoded[srcIdx + 3] < 255) {
                        // Alpha blend with previous frame
                        float srcA = decoded[srcIdx + 3] / 255.0f;
                        float dstA = frame[dstIdx + 3] / 255.0f;
                        float outA = srcA + dstA * (1 - srcA);
                        for (int c = 0; c < 4; ++c) {
                            float srcC = decoded[srcIdx + c] / 255.0f;
                            float dstC = frame[dstIdx + c] / 255.0f;
                            float outC = (srcC * srcA + dstC * dstA * (1 - srcA)) / (outA > 0 ? outA : 1);
                            frame[dstIdx + c] = static_cast<uint8_t>(outC * 255.0f + 0.5f);
                        }
                    }
                    else {
                        // No blend, just copy
                        std::memcpy(&frame[dstIdx], &decoded[srcIdx], 4);
                    }
                }
            }

            // Handle dispose method
            if (iter.dispose_method == WEBP_MUX_DISPOSE_BACKGROUND) {
                // If the next frame needs a cleared region, clear it in prevFrame
                for (int y = 0; y < decodeHeight; ++y) {
                    int destY = iter.y_offset + y;
                    if (destY < 0 || destY >= int(m_height)) continue;
                    for (int x = 0; x < decodeWidth; ++x) {
                        int destX = iter.x_offset + x;
                        if (destX < 0 || destX >= int(m_width)) continue;
                        size_t dstIdx = (destY * m_width + destX) * 4;
                        prevFrame[dstIdx + 0] = 0;
                        prevFrame[dstIdx + 1] = 0;
                        prevFrame[dstIdx + 2] = 0;
                        prevFrame[dstIdx + 3] = 0;
                    }
                }
            }
            else {
                // Otherwise, next frame starts from this one
                prevFrame = frame;
            }

            m_frames.push_back(std::move(frame));
            WebPFree(decoded);
        } while (WebPDemuxNextFrame(&iter));

        WebPDemuxReleaseIterator(&iter);
        WebPDemuxDelete(demux);
    }

    ~WebpImage() override = default;

    uint32_t getNumLayers() const override { return static_cast<uint32_t>(m_frames.size()); }
    uint32_t getNumMipmaps() const override { return 1; }
    uint32_t getWidth(uint32_t /*mipmap*/) const override { return m_width; }
    uint32_t getHeight(uint32_t /*mipmap*/) const override { return m_height; }
    uint32_t getDepth(uint32_t /*mipmap*/) const override { return 1; }
    gli::format getFormat() const override { return gli::format::FORMAT_RGBA8_SRGB_PACK8; }
    gli::format getOriginalFormat() const override { return m_originalFormat; }

    uint8_t* getData(uint32_t layer, uint32_t mipmap, size_t& size) override {
        size = m_width * m_height * 4;
        return m_frames[layer].data();
    }
    const uint8_t* getData(uint32_t layer, uint32_t mipmap, size_t& size) const override {
        size = m_width * m_height * 4;
        return m_frames[layer].data();
    }

private:
    std::vector<uint8_t> m_buffer;
    std::vector<std::vector<uint8_t>> m_frames;
    uint32_t m_width = 0, m_height = 0, m_frameCount = 0;
    bool m_hasAlpha = false;
    gli::format m_originalFormat = gli::format::FORMAT_RGBA8_SRGB_PACK8;
};

std::unique_ptr<image::IImage> webp_load(const char* filename)
{
    return std::make_unique<WebpImage>(filename);
}