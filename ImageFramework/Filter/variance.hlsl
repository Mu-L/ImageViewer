#setting sepa, true
#setting title, Statistic Variance
#setting description, Caclulates the weighted statistical variance var(X) = E(X^2) - E(X)^2 of the pixel neighborhood defined by the radius (grayscale). The weighted kernel is the default Gaussian kernel e^-0.5*x^2/sigma (Large sigma ~ equal weights).
#setting type, dynamic

#param Radius, blur_radius, int, 20, 1
#param Sigma, sigma, float, 26, 0.001

#param Border Handling, border_handling, enum {Clamp; Repeat}, Clamp
#define BORDER_CLAMP 0
#define BORDER_REPEAT 1

// Simple Gauss-Kernel. Normalization is not included and must be
// done by dividing through the weight sum.
float kernel(int _offset)
{
	return exp(-0.5 * _offset * _offset / sigma);
}

float4 getPixel(int3 pos, int3 size)
{
	if(border_handling == BORDER_CLAMP)
		pos = clamp(pos, 0, size-1);
	else if(border_handling == BORDER_REPEAT)
		pos = int3(uint3(pos + blur_radius * size) % uint3(size)); // % is only well defined for positive numbers, since blur_radius > 1 and size > 1 the expression (pos + blur_radius * size) should be positive

	return src_image[texel(pos)];
}

float4 filter(int3 pixelCoord, int3 size)
{
	float muSum = 0.0;
	float muSqSum = 0.0;
	float weightSum = 0.0;
	
	for(int d = -blur_radius; d <= blur_radius; d++)
	{			
		float w = kernel(d);
		weightSum += w;
		int3 pos = d * filterDirection + pixelCoord;
		float g = dot(float3(0.299, 0.587, 0.114), getPixel(pos, size).rgb);
		muSum += w * g;
		muSqSum += w * (g * g);
	}
	
	muSum /= weightSum;
	muSqSum /= weightSum;
	float variance = muSqSum - (muSum * muSum);
	return float4(variance, variance, variance, 1.0);
}