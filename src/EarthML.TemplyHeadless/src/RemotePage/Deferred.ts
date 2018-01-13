

export class Deferred {
    resolve;
    reject;
    promise = new Promise<any>((resolve, reject) => { this.resolve = resolve; this.reject = reject });
}