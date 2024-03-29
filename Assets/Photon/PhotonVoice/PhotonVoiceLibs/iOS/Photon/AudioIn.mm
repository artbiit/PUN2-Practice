// Photon_Audio_In works in 2 modes.
// 1. Fills buffer provided in ..._Read call. Takes minimum cpu cycles in render callback. But requires ring buffer adding some latency
// 2. Pushes audio data via callback as soon as data is available. Minimal latency and ability to use "push" Photon Voice interface which is more efficient.
// Push mode enabled if CallbackData.pushCallback property set.

#import "AudioIn.h"

// Framework includes
#import <AVFoundation/AVAudioSession.h>

#define SAMPLE_RATE 48000

#define XThrowIfError(error, operation)                                                 \
do {                                                                                    \
if (error) {                                                                            \
throw [NSException                                                                      \
exceptionWithName:@"PhotonAudioException"                                               \
reason:[NSString stringWithFormat:@"%s (%i)", operation, (int)error] userInfo:nullptr]; \
}                                                                                       \
} while (0)

const int BUFFER_SIZE = 4096000;
NSMutableSet* handles = [[NSMutableSet alloc] init];

struct CallbackData {
    AudioUnit rioUnit;
    BOOL audioChainIsBeingReconstructed;
    float* ringBuffer;
    int ringWritePos;
    int ringReadPos;
    
    int pushHostID;
    Photon_IOSAudio_PushCallback pushCallback;
    
    CallbackData(): rioUnit(NULL), audioChainIsBeingReconstructed(false),
    ringBuffer(NULL), ringWritePos(0), ringReadPos(0), pushHostID(0), pushCallback(NULL) {}
    
    ~CallbackData() {
        free(ringBuffer);
    }
};

@interface Photon_Audio_In() {
@public
    CallbackData cd;
}
- (void)setupAudioSession;
- (void)setupIOUnit;
- (void)setupAudioChain;
@end


Photon_Audio_In* Photon_Audio_In_CreateReader(int deviceID) {
    Photon_Audio_In* handle = [[Photon_Audio_In alloc] init];
    handle->cd.ringBuffer = (float*)malloc(sizeof(float)*BUFFER_SIZE);
    [handles addObject:handle];
    [handle startIOUnit];
    return handle;
}

bool Photon_Audio_In_Read(Photon_Audio_In* handle, float* buf, int len) {
    CallbackData& cd = handle->cd;
    if (cd.ringReadPos + len > cd.ringWritePos) {
        return false;
    }
    if (cd.ringReadPos + BUFFER_SIZE < cd.ringWritePos) {
        cd.ringReadPos = cd.ringWritePos - BUFFER_SIZE;
    }
    
    int pos = cd.ringReadPos % BUFFER_SIZE;
    if (pos + len > BUFFER_SIZE) {
        int remains = BUFFER_SIZE - pos;
        memcpy(buf, cd.ringBuffer + pos, remains * sizeof(float));
        memcpy(buf + remains, cd.ringBuffer, (len - remains) * sizeof(float));
    } else {
        memcpy(buf, cd.ringBuffer + pos, len * sizeof(float));
    }
    cd.ringReadPos += len;
    
    // tone test
    //    static int tonePos = 0;
    //    for(int i = 0;i < len;i++) buf[i] = sin((tonePos++) / 16.0f)/4;
    
    return true;
}

Photon_Audio_In* Photon_Audio_In_CreatePusher(int hostID, int deviceID, Photon_IOSAudio_PushCallback callback) {
    Photon_Audio_In* handle = [[Photon_Audio_In alloc] init];
    handle->cd.pushCallback = callback;
    handle->cd.pushHostID = hostID;
    [handles addObject:handle];
    [handle startIOUnit];
    return handle;
}

void Photon_Audio_In_Destroy(Photon_Audio_In* handle) {
    [handle stopIOUnit];
    [handles removeObject:handle];
}

// Render callback function
static OSStatus	performRender (void                         *inRefCon,
                               AudioUnitRenderActionFlags 	*ioActionFlags,
                               const AudioTimeStamp 		*inTimeStamp,
                               UInt32 						inBusNumber,
                               UInt32 						inNumberFrames,
                               AudioBufferList              *ioData)
{
    OSStatus err = noErr;
    CallbackData& cd = *((CallbackData*)inRefCon);
    if (cd.audioChainIsBeingReconstructed == NO)
    {
        // we are calling AudioUnitRender on the input bus of AURemoteIO
        // this will store the audio data captured by the microphone in ioData
        err = AudioUnitRender(cd.rioUnit, ioActionFlags, inTimeStamp, 1, inNumberFrames, ioData);
        
        if (!err) {
            if (cd.pushCallback) {
                cd.pushCallback(cd.pushHostID, (float*)ioData->mBuffers[0].mData, inNumberFrames);
            } else {
                int pos = cd.ringWritePos % BUFFER_SIZE;
                if (pos + inNumberFrames > BUFFER_SIZE) {
                    int remains = BUFFER_SIZE - pos;
                    memcpy(cd.ringBuffer + pos, (float*)ioData->mBuffers[0].mData, remains * sizeof(float));
                    memcpy(cd.ringBuffer, (float*)ioData->mBuffers[0].mData + remains, (inNumberFrames - remains) * sizeof(float));
                } else {
                    memcpy(cd.ringBuffer + pos, (float*)ioData->mBuffers[0].mData, inNumberFrames * sizeof(float));
                }
                // tone test
                //for(int i = 0;i < inNumberFrames;i++) cd.ringBuffer[(cd.ringWritePos + i) % BUFFER_SIZE] = sin((cd.ringWritePos + i) / 16.0f)/4;
                cd.ringWritePos += inNumberFrames;
            }
        }
        
        // mute output buffer
        for (UInt32 i=0; i<ioData->mNumberBuffers; ++i)
            memset(ioData->mBuffers[i].mData, 0, ioData->mBuffers[i].mDataByteSize);
        
    }
    
    return err;
}

@implementation Photon_Audio_In

- (id)init
{
    if (self = [super init]) {
        [self setupAudioChain];
    }
    return self;
}


- (void)handleInterruption:(NSNotification *)notification
{
    try {
        UInt8 theInterruptionType = [[notification.userInfo valueForKey:AVAudioSessionInterruptionTypeKey] intValue];
        NSLog(@"Session interrupted > --- %s ---\n", theInterruptionType == AVAudioSessionInterruptionTypeBegan ? "Begin Interruption" : "End Interruption");
        
        if (theInterruptionType == AVAudioSessionInterruptionTypeBegan) {
            [self stopIOUnit];
        }
        
        if (theInterruptionType == AVAudioSessionInterruptionTypeEnded) {
            // make sure to activate the session
            NSError *error = nil;
            [[AVAudioSession sharedInstance] setActive:YES error:&error];
            if (nil != error) NSLog(@"AVAudioSession set active failed with error: %@", error);
            
            [self startIOUnit];
        }
    } catch (NSException* e) {
        NSLog(@"Error: %@\n", e);
    }
}


- (void)handleRouteChange:(NSNotification *)notification
{
    UInt8 reasonValue = [[notification.userInfo valueForKey:AVAudioSessionRouteChangeReasonKey] intValue];
    AVAudioSessionRouteDescription *routeDescription = [notification.userInfo valueForKey:AVAudioSessionRouteChangePreviousRouteKey];
    
    NSLog(@"Route change:");
    switch (reasonValue) {
        case AVAudioSessionRouteChangeReasonNewDeviceAvailable:
            NSLog(@"     NewDeviceAvailable");
            break;
        case AVAudioSessionRouteChangeReasonOldDeviceUnavailable:
            NSLog(@"     OldDeviceUnavailable");
            break;
        case AVAudioSessionRouteChangeReasonCategoryChange:
            NSLog(@"     CategoryChange");
            NSLog(@" New Category: %@", [[AVAudioSession sharedInstance] category]);
            break;
        case AVAudioSessionRouteChangeReasonOverride:
            NSLog(@"     Override");
            break;
        case AVAudioSessionRouteChangeReasonWakeFromSleep:
            NSLog(@"     WakeFromSleep");
            break;
        case AVAudioSessionRouteChangeReasonNoSuitableRouteForCategory:
            NSLog(@"     NoSuitableRouteForCategory");
            break;
        default:
            NSLog(@"     ReasonUnknown");
    }
    
    NSLog(@"Previous route:\n");
    NSLog(@"%@\n", routeDescription);
    NSLog(@"Current route:\n");
    NSLog(@"%@\n", [AVAudioSession sharedInstance].currentRoute);
}

- (void)handleMediaServerReset:(NSNotification *)notification
{
    NSLog(@"Media server has reset");
    cd.audioChainIsBeingReconstructed = YES;
    
    usleep(25000); //wait here for some time to ensure that we don't delete these objects while they are being accessed elsewhere
    
    [self setupAudioChain];
    [self startIOUnit];
    
    cd.audioChainIsBeingReconstructed = NO;
}

- (void)setupAudioSession
{
    try {
        // Configure the audio session
        AVAudioSession *sessionInstance = [AVAudioSession sharedInstance];
        
        // we are going to play and record so we pick that category
        NSError *error = nil;
        [sessionInstance setCategory:AVAudioSessionCategoryPlayAndRecord error:&error];
        XThrowIfError((OSStatus)error.code, "couldn't set session's audio category");
        
        // set the buffer duration to 5 ms
        NSTimeInterval bufferDuration = .005;
        [sessionInstance setPreferredIOBufferDuration:bufferDuration error:&error];
        XThrowIfError((OSStatus)error.code, "couldn't set session's I/O buffer duration");
        
        // set the session's sample rate
        //        [sessionInstance setPreferredSampleRate:44100 error:&error];
        //        XThrowIfError((OSStatus)error.code, "couldn't set session's preferred sample rate");
        
        // add interruption handler
        [[NSNotificationCenter defaultCenter] addObserver:self
                                                 selector:@selector(handleInterruption:)
                                                     name:AVAudioSessionInterruptionNotification
                                                   object:sessionInstance];
        
        // we don't do anything special in the route change notification
        [[NSNotificationCenter defaultCenter] addObserver:self
                                                 selector:@selector(handleRouteChange:)
                                                     name:AVAudioSessionRouteChangeNotification
                                                   object:sessionInstance];
        
        // if media services are reset, we need to rebuild our audio chain
        [[NSNotificationCenter defaultCenter]	addObserver:	self
                                                 selector:	@selector(handleMediaServerReset:)
                                                     name:	AVAudioSessionMediaServicesWereResetNotification
                                                   object:	sessionInstance];
        
        // activate the audio session
        [[AVAudioSession sharedInstance] setActive:YES error:&error];
        XThrowIfError((OSStatus)error.code, "couldn't set session active");
    }
    
    catch (NSException* e) {
        NSLog(@"Error returned from setupAudioSession: %@", e);
    }
    catch (...) {
        NSLog(@"Unknown error returned from setupAudioSession");
    }
    
    return;
}


- (void)setupIOUnit
{
    try {
        // Create a new instance of AURemoteIO
        
        AudioComponentDescription desc;
        desc.componentType = kAudioUnitType_Output;
        desc.componentSubType = kAudioUnitSubType_VoiceProcessingIO;
        desc.componentManufacturer = kAudioUnitManufacturer_Apple;
        desc.componentFlags = 0;
        desc.componentFlagsMask = 0;
        
        AudioComponent comp = AudioComponentFindNext(NULL, &desc);
        XThrowIfError(AudioComponentInstanceNew(comp, &cd.rioUnit), "couldn't create a new instance of AURemoteIO");
        
        //  Enable input and output on AURemoteIO
        //  Input is enabled on the input scope of the input element
        //  Output is enabled on the output scope of the output element
        
        UInt32 one = 1;
        XThrowIfError(AudioUnitSetProperty(cd.rioUnit, kAudioOutputUnitProperty_EnableIO, kAudioUnitScope_Input, 1, &one, sizeof(one)), "could not enable input on AURemoteIO");
        XThrowIfError(AudioUnitSetProperty(cd.rioUnit, kAudioOutputUnitProperty_EnableIO, kAudioUnitScope_Output, 0, &one, sizeof(one)), "could not enable output on AURemoteIO");
        
        // Explicitly set the input and output client formats
        
        int sampleRate = SAMPLE_RATE;
        int channels = 1;
        AudioStreamBasicDescription ioFormat;
        ioFormat.mSampleRate = sampleRate;
        ioFormat.mFormatID = kAudioFormatLinearPCM;
        ioFormat.mFormatFlags = kAudioFormatFlagsNativeEndian | kAudioFormatFlagIsPacked | kAudioFormatFlagIsFloat;
        ioFormat.mFramesPerPacket = 1;
        ioFormat.mChannelsPerFrame = channels;
        ioFormat.mBytesPerFrame = ioFormat.mBytesPerPacket = sizeof(float) * channels;
        ioFormat.mBitsPerChannel = sizeof(float) * 8;
        ioFormat.mReserved = 0;
        
        XThrowIfError(AudioUnitSetProperty(cd.rioUnit, kAudioUnitProperty_StreamFormat, kAudioUnitScope_Input, 0, &ioFormat, sizeof(ioFormat)), "couldn't set input stream format AURemoteIO");
        
        XThrowIfError(AudioUnitSetProperty(cd.rioUnit, kAudioUnitProperty_StreamFormat, kAudioUnitScope_Output, 1, &ioFormat, sizeof(ioFormat)), "couldn't set output stream format AURemoteIO");
        
        // Set the MaximumFramesPerSlice property. This property is used to describe to an audio unit the maximum number
        // of samples it will be asked to produce on any single given call to AudioUnitRender
        UInt32 maxFramesPerSlice = 4096;
        XThrowIfError(AudioUnitSetProperty(cd.rioUnit, kAudioUnitProperty_MaximumFramesPerSlice, kAudioUnitScope_Global, 0, &maxFramesPerSlice, sizeof(UInt32)), "couldn't set max frames per slice on AURemoteIO");
        
        // Get the property value back from AURemoteIO. We are going to use this value to allocate buffers accordingly
        UInt32 propSize = sizeof(UInt32);
        XThrowIfError(AudioUnitGetProperty(cd.rioUnit, kAudioUnitProperty_MaximumFramesPerSlice, kAudioUnitScope_Global, 0, &maxFramesPerSlice, &propSize), "couldn't get max frames per slice on AURemoteIO");
        
        // We need references to certain data in the render callback
        // This simple struct is used to hold that information
        
        // Set the render callback on AURemoteIO
        AURenderCallbackStruct renderCallback;
        renderCallback.inputProc = performRender;
        renderCallback.inputProcRefCon = &cd;
        XThrowIfError(AudioUnitSetProperty(cd.rioUnit, kAudioUnitProperty_SetRenderCallback, kAudioUnitScope_Input, 0, &renderCallback, sizeof(renderCallback)), "couldn't set render callback on AURemoteIO");
        
        // Initialize the AURemoteIO instance
        XThrowIfError(AudioUnitInitialize(cd.rioUnit), "couldn't initialize AURemoteIO instance");
    }
    
    catch (NSException* e) {
        NSLog(@"Error returned from setupIOUnit: %@", e);
    }
    catch (...) {
        NSLog(@"Unknown error returned from setupIOUnit");
    }
    
    return;
}

- (void)setupAudioChain
{
    [self setupAudioSession];
    [self setupIOUnit];
}

- (OSStatus)startIOUnit
{
    OSStatus err = AudioOutputUnitStart(cd.rioUnit);
    if (err) NSLog(@"couldn't start AURemoteIO: %d", (int)err);
    return err;
}

- (OSStatus)stopIOUnit
{
    OSStatus err = AudioOutputUnitStop(cd.rioUnit);
    if (err) NSLog(@"couldn't stop AURemoteIO: %d", (int)err);
    return err;
}

- (double)sessionSampleRate
{
    return [[AVAudioSession sharedInstance] sampleRate];
}

- (BOOL)audioChainIsBeingReconstructed
{
    return cd.audioChainIsBeingReconstructed;
}

@end
