// Exported object from Tuanjie library

export { TuanjiePlayerAbilityBase } from './src/main/ets/ability/TuanjiePlayerAbilityBase'
export { TuanjiePlayerView } from './src/main/ets/pages/components/TuanjiePlayerView'
export { EngineLifecycleGuard } from './src/main/ets/common/EngineLifecycleGuard'
export { TuanjieLog } from './src/main/ets/common/TuanjieLog'

export { POST_MESSAGE_TO_HOST } from './src/main/ets/workers/HostProxy'     // send message to host(aka ui or main) thread
export { POST_MESSAGE_TO_WORKER } from './src/main/ets/workers/WorkerProxy' // send message to worker(tuanjie main) thread

export { MSG_RECEIVER,
  LOAD_MODULE, UNLOAD_MODULE,
  REGISTER_MODULE, UNREGISTER_MODULE,
  REGISTER_HANDLER, UNREGISTER_HANDLER,
} from './src/main/ets/workers/MessageProcessor'

export { SDKManager } from './oh_modules/OHKitSdk/src/main/ets/components/sdk/SDKManager' // send message to worker(tuanjie main) thread