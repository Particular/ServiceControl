import { createRouter, createWebHashHistory, type RouteRecordRaw } from "vue-router";
import config from "./config";

function meta(item: { title: string }) {
  return { title: `${item.title} • ServicePulse` };
}

function addChildren(parent: RouteRecordSingleViewWithChildren, item: RouteItem){
  if(item.children){
    item.children.forEach(child => {
      const newItem: RouteRecordSingleViewWithChildren = ({
        path: child.path,
        name: `${item.path}/${child.path}`,
        meta: meta(child),
        component: child.component,
        children: [],
      });
      parent.children.push(newItem);

      if (child.redirect) newItem.redirect = child.redirect;
      if (child.alias) newItem.alias = child.alias;

      addChildren(newItem, child);
    })
  }
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

    addChildren(result, item);

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
