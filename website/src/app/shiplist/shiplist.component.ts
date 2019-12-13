import { Component, OnInit, Query } from '@angular/core';
import { HttpClient } from "@angular/common/http";
import * as _ from "lodash";

import { environment } from "../../environments/environment";
import { LocalisationService } from '../localisation.service';

type doubleGroupedShipList = {
  [id: string]: {
    [id: string]: ShipIndexEntry[]
  }
}

@Component({
  selector: 'app-shiplist',
  templateUrl: './shiplist.component.html',
  styleUrls: ['./shiplist.component.scss']
})
export class ShipListComponent implements OnInit {

  byRoles: doubleGroupedShipList = {};
  specials: doubleGroupedShipList = {};
  bySize: doubleGroupedShipList = {};

  private sizes = {
    shipSizes: ["Size 0", "Size 1", "Size 2", "Size 3", "Size 4", "Size 5", "Size 6"],
    vehicleSizes: ["Size 0", "Size 1", "Size 2", "Size 3", "Size 4", "Size 5", "Size 6"]
  };

  constructor(private $http: HttpClient, private localisationSvc: LocalisationService) { }

  ngOnInit() {
    this.$http.get<ShipIndexEntry[]>(`${environment.api}/ships.json`).subscribe(r => {
      r = r.filter(x => x.career != "@LOC_PLACEHOLDER");
      r = r.filter(x => x.dogFightEnabled);

      // Figure out our own roles and sub-roles rather than using CIG's career/roles
      r.forEach(s => {
        s.roles = _.flatMap(this.localisationSvc.getText(s.role).split(" / "), cigRole => {
          if (s.isGroundVehicle) return { role: "Ground vehicles", subRole: cigRole };
          if (s.isGravlevVehicle) return { role: "Gravlev vehicles", subRole: cigRole };
          return this.isRolePrefix(cigRole) ? cigRole.split(" ").filter(rr => !this.isSizePrefix(rr)).map(rr => { return { role: rr, subRole: cigRole }; }) : { role: cigRole, subRole: "General" }
        });

        if (s.isSpaceship) s.roles.push({ role: "Spaceships by size", subRole: this.sizes.shipSizes[s.size || 0] })
        if (s.isGroundVehicle) s.roles.push({ role: "Ground vehicles by size", subRole: this.sizes.vehicleSizes[s.size || 0] })
        if (s.isGravlevVehicle) s.roles.push({ role: "Gravlev vehicles by size", subRole: this.sizes.vehicleSizes[s.size || 0] })
      });

      // Group by role and sub-role, ships will appear in multiple groupings
      r.forEach(s => {
        s.roles.forEach(r => {
          if (!this.byRoles[r.role]) this.byRoles[r.role] = {};
          if (!this.byRoles[r.role][r.subRole]) this.byRoles[r.role][r.subRole] = [];
          this.byRoles[r.role][r.subRole].push(s);
        });
      });

      // Move special groupings
      ["Ground vehicles", "Gravlev vehicles"].forEach(
        grouping => {
          this.specials[grouping] = this.byRoles[grouping];
          delete this.byRoles[grouping];
        }
      );

      // Move into by size groupings
      ["Spaceships by size", "Ground vehicles by size", "Gravlev vehicles by size"].forEach(
        grouping => {
          this.bySize[grouping] = this.byRoles[grouping];
          delete this.byRoles[grouping];
        }
      );
    });
  }

  shipsInRole(role: { [id: string]: ShipIndexEntry[] }): string {
    let ships: string[] = [];
    _.map(role, subRole => ships = ships.concat(_.map(subRole, s => s.className.toLowerCase())));
    return ships.join(",");
  }

  shipsInSubRole(ships: ShipIndexEntry[]): string {
    return _.map(ships, s => s.className.toLowerCase()).join(",");
  }

  private isRolePrefix(role: string): boolean {
    if (role.startsWith("Light")) return true;
    if (role.startsWith("Medium")) return true;
    if (role.startsWith("Heavy")) return true;
    if (role.startsWith("Stealth")) return true;
    if (role.startsWith("Snub")) return true;
    return false;
  }

  private isSizePrefix(role: string): boolean {
    if (role.startsWith("Light")) return true;
    if (role.startsWith("Medium")) return true;
    if (role.startsWith("Heavy")) return true;
    if (role.startsWith("Snub")) return true;
    return false;
  }

}

interface ShipIndexEntry {
  jsonFilename: string;
  name: string;
  career: string;
  role: string;
  className: string;
  type: string;
  subType: string;
  dogFightEnabled: boolean;
  size?: number;
  isGroundVehicle: boolean;
  isGravlevVehicle: boolean;
  isSpaceship: boolean;

  // We add these fields as we parse what we download from the API
  roles: { role: string, subRole: string }[];
}
