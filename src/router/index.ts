import { createRouter, createWebHashHistory, type RouteRecordRaw } from "vue-router";
import config from "./config";

function meta(item: { title: string }) {
  return { title: `${item.title} • ServicePulse` };
}




export default function makeRouter() {


  const routes = config.map<RouteRecordRaw>((item) => {
    const result: RouteRecordRaw = {
      path: item.path,
      name: item.path,
      meta: meta(item),
      component: item.component,
      children: item.children?.map<RouteRecordRaw>((child) => ({
        path: child.path,
        name: `${item.path}/${child.path}`,
        meta: meta(child),
        component: child.component,
      })),
    };
    if (item.redirect) result.redirect = item.redirect;
    if (item.alias) result.alias = item.alias;
  
    return result;
  });
  
  const defaultRoute = window.defaultConfig.default_route;  
  if(!!defaultRoute && defaultRoute!=='/'){    
    routes.push({
      path: "/",
      redirect: defaultRoute,
    });
  }  
  
  return createRouter({
    history: createWebHashHistory(window.defaultConfig.base_url),
    routes: routes,
    strict: false,
  });
}
