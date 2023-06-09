#ifndef BURN_TIME
#define BURN_TIME

UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(float, _BurnTime)
UNITY_INSTANCING_BUFFER_END(Props)

 
void getBurnTime_float(in int InstanceID, out float burnTime) {
    burnTime = UNITY_ACCESS_INSTANCED_PROP(Props, _BurnTime);
}
#endif