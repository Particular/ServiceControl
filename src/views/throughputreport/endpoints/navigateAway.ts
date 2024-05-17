export default class NavigateAway {
  private resolve: (value: (PromiseLike<boolean> | boolean)) => void = () => {};

  public navigateGuard(): Promise<boolean> {
    return new Promise( (resolve) => {
      this.resolve = resolve;
    })
  }

  public proceed(){
    this.resolve(true);
  }

  public cancel(){
    this.resolve(false);
  }
}