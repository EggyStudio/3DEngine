// The fragment stage of a masked surface in the shadow pass, which cuts it out where the model
// pass cuts it, so its shadow has the same holes. Every other surface is drawn into the shadow map
// with no fragment stage at all.
import modelpass;

[shader("fragment")]
void fragmentMain(ModelVertexOutput input)
{
    // Called for the discard alone, below the material's cutoff.
    alphaTested(baseColor(input), input);
}
